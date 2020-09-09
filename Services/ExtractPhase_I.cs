using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing.ChartDrawing;
using Sumtotal.ConfigurationsAutomation.Contracts;
using Sumtotal.ConfigurationsAutomation.Data;
using Sumtotal.ConfigurationsAutomation.Utilities;
using SumTotal.Framework.Container;
using SumTotal.Framework.Data;
using SumTotal.Models.Core.Security;
using SumTotal.Repository.Contracts.Core.Person;
using SumTotal.Repository.Contracts.Infra;
using SumTotal.Services.DataContracts.Core.Lookups;
using SumTotal.Services.Facade.Contracts.Core.Lookups;
using SumTotal.Services.Facade.Contracts.Core.Person;
using SumTotal.Services.Jobs.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class ExtractPhase_I : BaseExtract, IExtractPhase_I
    {
        ServiceJobContext jobContext = new ServiceJobContext();
        IPersonFacade personFacade = SumtContainer.Resolve<IPersonFacade>();
        Dictionary<string, int> orgDomains = new Dictionary<string, int>();

        public ExtractPhase_I()
        {

        }

        public override void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInfo($"Execution Proces Started for {context.JobKey} at {currentTime}.");
            dataProvider = SumtContainer.Resolve<IDataProvider>();
            dataProvider.Open();

            try
            {

                string reportPath = parameters["ExtractPath"].ToString();
                string filePrefix = parameters["FilePrefix"].ToString();


                ISystemJobRepository systemJobRepository = SumtContainer.Resolve<ISystemJobRepository>();
                ICodeDefinitionFacade facade = SumtContainer.Resolve<ICodeDefinitionFacade>();
                CodeDefinitionDTO codeDefinition = facade.GetCodeDefinitionWithAttributes(_configurationParameters.MasterCategoryCode);

                var finalPath = Path.Combine(reportPath, filePrefix + "_" + DateTime.Now.ToString("yyyyMMdd'_'HHmmss") + ".xlsx");

                IList<CodeDTO> codes = codeDefinition.Codes.Where(c => c.CodeAttributeDTO.Attr1Val.Equals(_configurationParameters.Group)).ToList();
                if (codes.Count == 0)
                {
                    _logger.LogError("Please recheck master category code in system job parameters");
                    return;
                }
                var workbook = new XLWorkbook();
                foreach (CodeDTO code in codes)
                {
                    CodeDefinitionDTO sectionCode = facade.GetCodeDefinitionWithAttributes(Convert.ToInt32(code.ItemCode));
                    try
                    {
                        var sectionData = sectionCode.Codes.Select(c => new SectionTemplate()
                        {
                            SettingName = c.CodeAttributeDTO.Attr1Val,
                            SettingKey = c.ItemCode,
                            Sequence = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr2Val) ? 0 : Convert.ToInt32(c.CodeAttributeDTO.Attr2Val),
                            HasLookup = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr3Val) ? false : Convert.ToBoolean(c.CodeAttributeDTO.Attr3Val),
                            IsPerson = c.CodeAttributeDTO.Attr4Val,
                            IsRule = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr5Val) ? false : Convert.ToBoolean(c.CodeAttributeDTO.Attr5Val),
                            MergeColumns = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr6Val) ? 0 : Convert.ToInt32(c.CodeAttributeDTO.Attr6Val),
                            LookupData = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr7Val) ? "" : c.CodeAttributeDTO.Attr7Val.ToString()
                        }).ToList();

                        String[] sectionLookups = code.CodeAttributeDTO.Attr2Val.Split(',');
                        string sectionLookupClause = string.Empty;
                        for (int i = 0; i < sectionLookups.Length; i++)
                        {
                            sectionLookups[i] = " p.Section like '%" + sectionLookups[i] + "%'";
                        }
                        sectionLookupClause = String.Join(" OR ", sectionLookups);
                        bool checkOnlyGlobal = false;
                        if (sectionLookupClause.Contains("GeneralSetting")) checkOnlyGlobal = true;
                        var tupleSectionData = PullSectionSettingvaluesIntoTemp(sectionLookupClause, checkOnlyGlobal);
                        DataTable settingsData = JoinTwoDataTablesOnOneColumn(Helper.ToDataTable(sectionData), tupleSectionData, "SettingKey");
                        workbook = Helper.AddWorkSheet(settingsData, code.ItemText, workbook);
                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError("No codes for catcd " + sectionCode.CodeName);
                    }
                }

                foreach (IXLWorksheet xLWorksheet in workbook.Worksheets)
                {

                    xLWorksheet.Style.Border.RightBorder = XLBorderStyleValues.Thick;
                    xLWorksheet.Style.Border.RightBorderColor = XLColor.Black;
                    xLWorksheet.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
                    var rows = xLWorksheet.RangeUsed().RowsUsed().Skip(1);
                    // First possible address of the company table:
                    var firstPossibleAddress = xLWorksheet.Row(1).FirstCell().Address;
                    // Last possible address of the company table:
                    var lastPossibleAddress = xLWorksheet.LastCellUsed().Address;

                    // Get a range with the remainder of the worksheet data (the range used)
                    var companyRange = xLWorksheet.Range(firstPossibleAddress, lastPossibleAddress).RangeUsed();

                    var firstTableCell = xLWorksheet.FirstCellUsed();
                    var lastTableCell = xLWorksheet.LastCellUsed();
                    xLWorksheet.Style.Font.FontSize = 9;
                    var rngData = xLWorksheet.Range(firstTableCell.Address, lastTableCell.Address);

                    rngData.Rows(); // From all rows
                    rngData.Style.Border.SetInsideBorder(XLBorderStyleValues.Thin);
                    var range = xLWorksheet.Range("A1:" + lastTableCell.Address.ColumnLetter + "1");
                    range.Style.Font.FontSize = 11;
                    range.Style.Font.FontColor = XLColor.White;
                    range.Style.Fill.BackgroundColor = XLColor.RichElectricBlue;

                    xLWorksheet.Columns(2, 2).Style.Alignment.WrapText = true;

                    foreach (var row in rows)
                    {
                        if (row.Cell(1).Value.ToString().Contains("PageBreak"))
                        {
                            var rowNumber = row.RowNumber();
                            string pagebreakValue = xLWorksheet.Cell(rowNumber, 2).Value.ToString();
                            string[] splitValue = pagebreakValue.Split(':');
                            if (splitValue.Length == 2)
                            {
                                xLWorksheet.Cell(rowNumber, 2).Value = splitValue[0].ToString();
                                xLWorksheet.Cell(rowNumber, 2).Style.Font.FontSize = 10;
                                xLWorksheet.Range("C" + rowNumber.ToString() + ":" + lastTableCell.Address.ColumnLetter + rowNumber.ToString()).Row(1).Merge();
                                xLWorksheet.Range("C" + rowNumber.ToString() + ":" + lastTableCell.Address.ColumnLetter + rowNumber.ToString()).Style.Font.FontSize = 10;
                                xLWorksheet.Cell("C" + rowNumber.ToString()).Value = splitValue[1].ToString();
                            }
                            xLWorksheet.Cell(rowNumber, 2).Style.Font.Bold = true;
                        }
                        if (row.Cell(1).Value.ToString().Contains("EmptyRow"))
                        {
                            var rowNumber = row.RowNumber();

                            xLWorksheet.Cell(rowNumber, 2).Value = "";
                               
                            
                        }
                    }

                    xLWorksheet.Column(1).Delete();
                    //xLWorksheet.ColumnWidth = 28;
                    //xLWorksheet.Columns(1, 1).Style.Alignment.WrapText = true;
                    
                    xLWorksheet.Columns(1, lastTableCell.Address.ColumnNumber).Width = 35;
                    xLWorksheet.Style.Alignment.WrapText = true;
                    xLWorksheet.Columns(1, 1).Width = 50;
                    xLWorksheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    xLWorksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    //xLWorksheet.Style.Alignment.SetVertical(XLDrawingVerticalAlignment.Bottom);
                    //xLWorksheet.Comment.Style
                    //  .Alignment.SetVertical(XLDrawingVerticalAlignment.Bottom)
                    //  .Alignment.SetHorizontal(XLDrawingHorizontalAlignment.Right);
                    
                    // xLWorksheet.ColumnWidth = 60;
                }
                workbook.SaveAs(finalPath);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while processing configurations extract.", ex);
                throw (ex);
            }
            finally
            {
                dataProvider.Close();
            }
        }
        public override void ExecuteImport(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInfo($"Execution Proces Started for {context.JobKey} at {currentTime}.");
            dataProvider = SumtContainer.Resolve<IDataProvider>();
            dataProvider.Open();
            try
            {
                string importConfigFilePath = parameters["ImportConfigFilepath"].ToString();
                string OrgFilePath = parameters["OrganizationListFilePath"].ToString();
                DataTable dtOrgnization = Helper.FileContentToDataTable(OrgFilePath, true);
                dtOrgnization.PrimaryKey = new DataColumn[] {
                    dtOrgnization.Columns["code"]
                };
                DataTable dtpersist = Helper.FileContentToDataTable(importConfigFilePath, false);
                DataTable dtDBOrg = GetOrgDetails();
                UpdatePersist(dtpersist, dtOrgnization, dtDBOrg);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while processing configurations extract.", ex);
                throw (ex);
            }
            finally
            {
                dataProvider.Close();
            }
        }
        public override void ExecuteExport(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInfo($"Execution Proces Started for {context.JobKey} at {currentTime}.");
            dataProvider = SumtContainer.Resolve<IDataProvider>();
            dataProvider.Open();

            try
            {
                string settingstoExport = string.Empty;
                //Load job parameters
                settingstoExport = parameters["SettingsExport"].ToString();
                string reportPath = parameters["ExtractPath"].ToString();

                ISystemJobRepository systemJobRepository = SumtContainer.Resolve<ISystemJobRepository>();
                ICodeDefinitionFacade facade = SumtContainer.Resolve<ICodeDefinitionFacade>();
                CodeDefinitionDTO codeDefinition = facade.GetCodeDefinitionWithAttributes(_configurationParameters.MasterCategoryCode);

                IList<CodeDTO> codes = codeDefinition.Codes.Where(c => c.CodeAttributeDTO.Attr1Val.Equals(_configurationParameters.Group)).ToList();
               // codeDefinition.Codes.Where(c => c.ItemText.Equals(settingstoExport)).ToList();
                string sectionLookupClause = string.Empty;
                foreach (CodeDTO code in codes)
                {
                    String[] sectionLookups = code.CodeAttributeDTO.Attr2Val.Split(',');
                    for (int i = 0; i < sectionLookups.Length; i++)
                    {
                        sectionLookups[i] = " p.Section like '%" + sectionLookups[i] + "%'";
                    }
                    sectionLookupClause += String.Join(" OR ", sectionLookups);
                }
                orgDomains = GetOrgDetails(_configurationParameters.Domains);
                foreach(KeyValuePair<string,int> kvp in orgDomains)
                {
                    GetPersistData(reportPath, sectionLookupClause, kvp);
                }
               
            }
            catch (Exception ex)
            {
                _logger.LogError("Error while processing configurations extract.", ex);
                throw (ex);
            }
            finally
            {
                dataProvider.Close();
            }

        }
        private void GetPersistData(string reportPath, string sectionClause, KeyValuePair<string, int> domainCode)
        {
            try
            {
                string sqlCommand = @"SELECT rtrim(ltrim(cast(App as varchar(50)))) +','+ Scope +','+Section +','+replace(Data,char(13)+char(10),'<<<<>>>') as settings from Persist p
                             where " + sectionClause;
                sqlCommand += " Section like '%" + domainCode.Value + "%'";
             
                DataSet dataSet = dataProvider.ExecuteSelectSql(sqlCommand);
                DataTable dt = dataSet.Tables[0];
                var finalPath = Path.Combine(reportPath, "Persist_ApprovalConfig_"+ domainCode.Key +"_"+ DateTime.Now.ToString("yyyyMMdd'_'HHmmss") + ".txt");
                IEnumerable<string> columnNames = dt.Columns.Cast<DataColumn>().
                                  Select(column => column.ColumnName);
                StringBuilder sb = new StringBuilder();
                //sb.AppendLine(string.Join(",", columnNames));
                foreach (DataRow row in dt.Rows)
                {
                    string[] fields = row.ItemArray.Select(field => field.ToString()).
                                                    ToArray();
                    sb.AppendLine(string.Join(",", fields));
                }
                File.WriteAllText(finalPath, sb.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error Occoured in ClassName {nameof(Export)} Method {nameof(GetPersistData)}" + ex.Message);

            }
        }

        private DataTable PullSectionSettingvaluesIntoTemp(string lookupClause, bool checkOnlyGlobal = false)
        {
            string sqlCommand = string.Empty;
            if (checkOnlyGlobal)
            {
                sqlCommand = ConfigurationConstants.PHASEI_QueryTemplateForGlobalOnly.Replace("##SectionLookups##", lookupClause);                
            }
            else
            {
                sqlCommand = ConfigurationConstants.PHASEI_QueryTemplate.Replace("##SectionLookups##", lookupClause);
            }

            DataSet dataSet = dataProvider.ExecuteSelectSql(sqlCommand);
            DataTable OrgHierarchy = new DataTable();
            DataTable sectionResult = new DataTable();
            OrgHierarchy = dataSet.Tables[0];
            sectionResult = dataSet.Tables[1];

            // ds.WriteXml(Path.Combine(Environment.CurrentDirectory, "Product.xml"));
            var SelectedValues = OrgHierarchy.AsEnumerable().Select(s => s.Field<string>("Name")).Distinct().ToArray();

            string commaSeperatedValues = string.Join(",", SelectedValues);
            commaSeperatedValues = commaSeperatedValues + ",";
            //string newList = string.Join(",", commaSeperatedValues.Split(',').Select(x => string.Format("[{0}]", x)).ToList());

            string newList = string.Join(",", commaSeperatedValues.Split(',').Select(x => string.Format("Max([{0}]) as [{0}]", x)).ToList());

            var data2 = sectionResult.AsEnumerable().Select(x => new
            {
                settingkey = x.Field<String>("settingkey"),
                settingvalue = x.Field<String>("settingvalue"),
                name = x.Field<String>("name")
            });

            DataTable dt3 = new DataTable();
            DataColumn pc = new DataColumn("name", typeof(String));
            DataColumn pv = new DataColumn("settingvalue", typeof(String));

            for (int i = 0; i < sectionResult.Rows.Count; i++)
            {
                for (int j = 0; j < sectionResult.Columns.Count; j++)
                {
                    if (string.IsNullOrEmpty(sectionResult.Rows[i][j].ToString()))
                    {
                        // Write your Custom Code
                        sectionResult.Rows[i][j] = "N/A";
                    }
                }
            }

            DataTable temp = sectionResult.Copy();

            temp.Columns.Remove("OrganizationPK");
            temp.Columns.Remove("section");
            temp.Columns.Remove("DataItem");
            dt3 = Pivot(temp, pc, pv);


            //var data=dt.AsEnumerable().Top
            //Max([sampathDomain]) as[sampathDomain]
            return dt3;

        }
        public static DataTable JoinTwoDataTablesOnOneColumn(DataTable dtblLeft, DataTable dtblRight, string colToJoinOn)
        {
            //Change column name to a temp name so the LINQ for getting row data will work properly.
            string strTempColName = "settingkey_db";
            IPersonRepository personRepository = SumtContainer.Resolve<IPersonRepository>();

            if (dtblRight.Columns.Contains(colToJoinOn))
                dtblRight.Columns[colToJoinOn].ColumnName = strTempColName;

            //Get columns from dtblLeft
            DataTable dtblResult = dtblLeft.Clone();
            dtblResult.Columns["Sequence"].DataType = typeof(Int32);

            //Get columns from dtblRight
            var dt2Columns = dtblRight.Columns.OfType<DataColumn>().Select(dc => new DataColumn(dc.ColumnName, dc.DataType, dc.Expression, dc.ColumnMapping));

            //Get columns from dtblRight that are not in dtblLeft
            var dt2FinalColumns = from dc in dt2Columns.AsEnumerable()
                                  where !dtblResult.Columns.Contains(dc.ColumnName)
                                  select dc;

            //Add the rest of the columns to dtblResult
            dtblResult.Columns.AddRange(dt2FinalColumns.ToArray());


            //No reason to continue if the colToJoinOn does not exist in both DataTables.
            if (!dtblLeft.Columns.Contains(colToJoinOn) || (!dtblRight.Columns.Contains(colToJoinOn) && !dtblRight.Columns.Contains(strTempColName)))
            {
                if (!dtblResult.Columns.Contains(colToJoinOn))
                    dtblResult.Columns.Add(colToJoinOn);
                return dtblResult;
            }

            dtblLeft.DefaultView.Sort = colToJoinOn;
            dtblLeft = dtblLeft.DefaultView.ToTable();

            dtblRight.DefaultView.Sort = strTempColName;
            dtblRight = dtblRight.DefaultView.ToTable();

            var rowDataLeftOuter = from rowLeft in dtblLeft.AsEnumerable()
                                   join rowRight in dtblRight.AsEnumerable() on rowLeft[colToJoinOn].ToString().ToLower().Trim() equals rowRight[strTempColName].ToString().ToLower().Trim() into gj
                                   from subRight in gj.DefaultIfEmpty()
                                   select rowLeft.ItemArray.Concat((subRight == null) ? (dtblRight.NewRow().ItemArray) : subRight.ItemArray).ToArray();


            foreach (object[] values in rowDataLeftOuter)
            {
                dtblResult.Rows.Add(values);
            }



            foreach (DataRow dr in dtblResult.Rows)
            {

                if (Convert.ToBoolean(dr.Field<String>("HasLookup")))
                {
                    var lookupData = new Dictionary<String, string>();
                    var list = dr.Field<string>("LookupData").ToString().Split(';').ToList();
                    if (list.Count > 0)
                    {
                        list.ForEach(l =>
                        {
                            var item = l.Split(':');
                            if (item.Length > 1)
                                lookupData.Add(item[0], item[1]);
                        });
                        for (int c = 1; c < dt2Columns.Count(); c++)
                        {
                            var values = dr.Field<string>(dt2Columns.ElementAt(c).ColumnName);
                            if (values != null)
                            {
                                //Any specific char want remove                                
                                values = values.Trim(new Char[] { '\'' });
                                List<String> valueFromLookup = new List<string>();
                                foreach (var value in values.Split(','))
                                {
                                    if (value != null && lookupData.ContainsKey(value))
                                        valueFromLookup.Add(lookupData[value].ToString());
                                }
                                dr[dt2Columns.ElementAt(c).ColumnName] = String.Join(",", valueFromLookup);
                            }
                        }
                    }
                }
                for (int i = 1; i <= dt2Columns.Count() - 1; i++)
                {
                    var value = dr.Field<string>(dt2Columns.ElementAt(i).ColumnName);
                    if (value != null && value.Equals("NOTAPL"))
                        dr[dt2Columns.ElementAt(i).ColumnName] = String.Empty;
                }

                if (!String.IsNullOrEmpty(dr.Field<String>("IsPerson").Trim()))
                {
                    DataRow row = dtblResult.Rows
                             .Cast<DataRow>()
                             .Where(x => x[colToJoinOn].ToString().Equals(dr.Field<String>("IsPerson").ToString(), StringComparison.OrdinalIgnoreCase)).ToList().FirstOrDefault();
                    for (int c = 1; c < dt2Columns.Count() - 1; c++)
                    {
                        //DataRow row = dtblResult.Select()

                        var value = row.Field<string>(dt2Columns.ElementAt(c).ColumnName);
                        if (value != null && value.ToString() != "0")
                        {
                            if (Int32.TryParse(value.ToString(), out int PersonPk))
                            {
                                Person personInfo = personRepository.LoadPersonById(PersonPk);
                                if (personInfo != null)
                                    dr[dt2Columns.ElementAt(c).ColumnName] = personInfo.PersonNumber;
                            }
                        }
                    }
                }
            }

            var personRecords = dtblResult.Rows
                             .Cast<DataRow>()
                             .Where(x => x["IsPerson"].ToString().Trim().Length > 0)
                             .Select(p => p.Field<string>("IsPerson")).ToList();

            var rowsToBeDeleted = dtblResult.Rows
                 .Cast<DataRow>()
                 .Where(x => personRecords.Contains(x[strTempColName].ToString()))
                 .ToList();

            foreach (DataRow row in rowsToBeDeleted)
            {
                dtblResult.Rows.Remove(row);
            }

            dtblResult.AcceptChanges();

            //Change column name back to original
            dtblRight.Columns[strTempColName].ColumnName = colToJoinOn;
            
            dtblResult.DefaultView.Sort = "Sequence";
            dtblResult = dtblResult.DefaultView.ToTable();
            dtblResult.AcceptChanges();
            //Remove extra column from result and setting name also
            dtblResult.Columns.Remove(strTempColName);
            dtblResult.Columns.Remove("Sequence");
            dtblResult.Columns.Remove("HasLookup");
            dtblResult.Columns.Remove("IsPerson");
            dtblResult.Columns.Remove("IsRule");
            dtblResult.Columns.Remove("MergeColumns");
            dtblResult.Columns.Remove("LookupData");

            return dtblResult;

        }


        private static DataTable Pivot(DataTable dt, DataColumn pivotColumn, DataColumn pivotValue)
        {
            // find primary key columns 
            //(i.e. everything but pivot column and pivot value)
            DataTable temp = dt.Copy();
            temp.Columns.Remove(pivotColumn.ColumnName);
            temp.Columns.Remove(pivotValue.ColumnName);
            string[] pkColumnNames = temp.Columns.Cast<DataColumn>()
                .Select(c => c.ColumnName)
                .ToArray();

            // prep results table
            DataTable result = temp.DefaultView.ToTable(true, pkColumnNames).Copy();

            result.PrimaryKey = result.Columns.Cast<DataColumn>().ToArray();
            dt.AsEnumerable()
                .Select(r => r[pivotColumn.ColumnName].ToString())
                .Distinct().ToList()
                .ForEach(c => result.Columns.Add(c, pivotColumn.DataType));

            // load it
            foreach (DataRow row in dt.Rows)
            {
                // find row to update
                DataRow aggRow = result.Rows.Find(
                    pkColumnNames
                        .Select(c => row[c])
                        .ToArray());
                // the aggregate used here is LATEST 
                // adjust the next line if you want (SUM, MAX, etc...)
                aggRow[row[pivotColumn.ColumnName].ToString()] = row[pivotValue.ColumnName];
            }

            return result;
        }

        private Dictionary<string,int> GetOrgDetails(string[] orgCodes)
        {
            try
            {
                DataTable dt = new DataTable();
                string domainsToLookup = null;
                string executableQuery = "Select distinct OrganizationPK, code from Organization WHERE OrgDomainInd = 1 and Deleted = 0";
                if (orgCodes.Length>0)
                {
                    domainsToLookup = string.Join(",", orgCodes.Select(x => $"'{x}'"));
                    executableQuery += " AND Code in (" + domainsToLookup + ")";
                }

                DataSet dataSet = dataProvider.ExecuteSelectSql(executableQuery);
                dt = dataSet.Tables[0];
                dt.PrimaryKey = new DataColumn[] {
                    dt.Columns["code"]
                };
                return dt.AsEnumerable()
                      .ToDictionary<DataRow, string, int>(row => row.Field<string>(1),
                       row => row.Field<int>(0));
            }
            catch(Exception ex)
            {
                _logger.LogError($"Error Occoured in ClassName {nameof(ExtractPhase_I)} Method {nameof(GetOrgDetails)}" + ex.Message);
                _logger.LogError($"Please check Attr3Val in the configuration category.");
                return null;
            }
            
        }

        private void UpdatePersist(DataTable persistData, DataTable dtOrgnization, DataTable dtDBOrg)
        {
            var query = (from fileorg in dtOrgnization.AsEnumerable()
                         join dbOrg in dtDBOrg.AsEnumerable()
                         on fileorg.Field<string>("code") equals dbOrg.Field<string>("code")
                         select new
                         {
                             fileCode = fileorg.Field<string>("code"),
                             Dbcode = dbOrg.Field<string>("code"),
                             fileOrgPk = Convert.ToInt32(fileorg.Field<string>("organizationpk")),
                             dbOrgPk = dbOrg.Field<int>("organizationpk")
                         }).ToList();

            foreach (DataRow oRow in persistData.Rows)
            {
                string spliltScope = oRow.ItemArray[2].ToString();
                string[] scope = spliltScope.Split(new string[] { "/" }, StringSplitOptions.None);
                if (scope.Length == 3)
                {
                    var items = query.Where(f => f.fileOrgPk == Convert.ToInt32(scope[1])).Select(n => n.dbOrgPk).ToList<int>();
                    if (items.Count != 0)
                    {
                        string finalScope = scope[0] + "/" + items[0] + "/" + scope[2];

                        Dictionary<string, object> spParams = new Dictionary<string, object>()
                    {
                    {"@app", oRow.ItemArray[0].ToString() },
                    {"@scope", oRow.ItemArray[1].ToString()},
                    {"@section", finalScope },
                    {"@data", oRow.ItemArray[3].ToString()}
                    };
                        try
                        {
                            string sqlCommand = "Update persist set data = replace( @data, '<<<<>>>', char(13) + char(10)) where app = @app  AND scope = @scope  AND Section = @section";
                            dataProvider.ExecuteNonQueryStmt(sqlCommand, spParams);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError("Error while updating system job parameter.", ex);
                            throw (ex);
                        }
                    }


                }

            }
        }

        private DataTable GetOrgDetails()
        {
            DataTable dt = new DataTable();

            DataSet dataSet = dataProvider.ExecuteSelectSql("Select OrganizationPK,code from Organization");
            dt = dataSet.Tables[0];
            dt.PrimaryKey = new DataColumn[] {
                    dt.Columns["code"]
                };
            return dt;
        }
    }
}

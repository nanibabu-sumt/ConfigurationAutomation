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
using System.Runtime.InteropServices;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class ExtractPhase_II : BaseExtract, IExtractPhase_II
    {
        public override void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string currentTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            _logger.LogInfo($"Execution Proces Started for {context.JobKey} at {currentTime}.");
            dataProvider = SumtContainer.Resolve<IDataProvider>();
            dataProvider.Open();
            try
            {
                int masterCategoryCode;
                string group;

                //Load job parameters
                masterCategoryCode = Convert.ToInt32(parameters["MasterCategoryCode"]);
                group = parameters["Group"].ToString();
                string reportPath = parameters["ExtractPath"].ToString();
                string filePrefix = parameters["FilePrefix"].ToString();
                //added new line  code test

                ISystemJobRepository systemJobRepository = SumtContainer.Resolve<ISystemJobRepository>();
                ICodeDefinitionFacade facade = SumtContainer.Resolve<ICodeDefinitionFacade>();
                CodeDefinitionDTO codeDefinition = facade.GetCodeDefinitionWithAttributes(masterCategoryCode);

                var finalPath = Path.Combine(reportPath, filePrefix + "_" + DateTime.Now.ToString("yyyyMMdd'_'HHmmss") + ".xlsx");

                IList<CodeDTO> codes = codeDefinition.Codes.Where(c => c.CodeAttributeDTO.Attr1Val.Equals(group)).ToList();
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
                            Itmtxt = c.ItemText,
                            Sequence = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr2Val) ? 0 : Convert.ToInt32(c.CodeAttributeDTO.Attr2Val),
                            CodeLookupCatcd = String.IsNullOrEmpty(c.CodeAttributeDTO.Attr4Val) ? "" : c.CodeAttributeDTO.Attr4Val.ToString()
                        }).ToList();

                        DataTable dt2 = new DataTable();
                        dt2.Columns.Add("Securitykey", typeof(String));
                        dt2.Columns.Add("SecurityName", typeof(String));
                        dt2.Columns.Add("bitvalue", typeof(String));
                        dt2.Columns.Add("Sequence", typeof(Decimal));

                        object[] values = new object[4];


                        foreach (SectionTemplate sec in sectionData)
                        {

                            values[0] = sec.SettingKey;
                            values[1] = sec.SettingKey;
                            values[3] = sec.Sequence;
                            dt2.Rows.Add(values);
                            string[] securitykeys = sec.SettingName.Split(',');
                            int i = 0;
                            // Part 3: loop over result array.
                            foreach (string securitykey in securitykeys)
                            {
                                if (securitykeys[0] != "")
                                        {
                                    values[0] = securitykey;
                                    values[1] = sec.SettingKey;
                                    values[3] = sec.Sequence + "." + i;
                                    dt2.Rows.Add(values);
                                    i++;
                                }

                            }

                        }

                        //Get Security name hexadecimal value and bit value
                        CodeDefinitionDTO hexadecimal = facade.GetCodeDefinitionWithAttributes(Convert.ToInt32("100900"));

                        var hexadecimalData = hexadecimal.Codes.Select(c => new RoleMaskSettings()
                        {
                            SecurityName = c.ItemCode,                           
                            HexaDecimal = c.CodeAttributeDTO.Attr1Val,
                            bitvalue = c.CodeAttributeDTO.Attr2Val,
                            Itmtxt = c.ItemText
                        }).ToList();

                        //convert list to data table
                        DataTable code100900 = new DataTable();
                        code100900 = Helper.ToDataTable(hexadecimalData);

                        dt2.AsEnumerable()
                            .Join(code100900.AsEnumerable(),
                           dt1_Row => dt1_Row.ItemArray[0],
                           dt2_Row => dt2_Row.ItemArray[0],
                           (dt1_Row, dt2_Row) => new { dt1_Row, dt2_Row })
                           .ToList()
                           .ForEach(o =>
                                   o.dt1_Row.SetField(2, o.dt2_Row.ItemArray[2]));

                        dt2.AsEnumerable()
                           .Join(code100900.AsEnumerable(),
                          dt1_Row => dt1_Row.ItemArray[0],
                          dt2_Row => dt2_Row.ItemArray[0],
                          (dt1_Row, dt2_Row) => new { dt1_Row, dt2_Row })
                          .ToList()
                          .ForEach(o =>
                                  o.dt1_Row.SetField(0, o.dt2_Row.ItemArray[3]));

                        Tuple<DataTable,DataTable> RoleMaskOrgData = GetRoleMaskData();
                        DataTable RoleMaskData = RoleMaskOrgData.Item1;
                        DataTable OrgData = RoleMaskOrgData.Item2;
                        var distinctIds = RoleMaskData.AsEnumerable()
                                .Select(s => new {
                                    id = s.Field<int>("Role_DomainFK"),
                                })
                                .Distinct().ToList();
                        for (int i = 3; i < distinctIds.Count + 3; i++)
                        {
                            DataTable rolemaskDomainData = new DataTable();
                            int domainId = Int32.Parse(distinctIds[i - 3].id.ToString());
                            string Domian_Name = null;
                            var DomainName = from row in OrgData.AsEnumerable()
                                             where row.Field<int>("OrganizationPK") == domainId
                                             select new
                                             {
                                                 DomainName = row.Field<string>("Name")
                                             };

                            rolemaskDomainData = GetRolemaskDetails(domainId, RoleMaskData, dt2);
                            rolemaskDomainData.Columns.Remove("bitvalue");
                            rolemaskDomainData.Columns.Remove("Sequence");

                            foreach(var dom in DomainName)
                            {
                                Domian_Name = dom.DomainName;
                            }
                            workbook = Helper.AddWorkSheet(rolemaskDomainData, Domian_Name, workbook);
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

                           

                            foreach (var row in rows)
                            {
                                if (row.Cell(1).Value.ToString()== row.Cell(2).Value.ToString())
                                {
                                    var rowNumber = row.RowNumber();
                                    string pagebreakValue = xLWorksheet.Cell(rowNumber, 2).Value.ToString();                                   
                                    xLWorksheet.Cell(rowNumber, 1).Style.Font.Bold = true;
                                }
                               
                            }

                            xLWorksheet.Column(2).Delete();                           

                            xLWorksheet.Columns(1, lastTableCell.Address.ColumnNumber).Width = 35;
                            xLWorksheet.Style.Alignment.WrapText = true;
                            xLWorksheet.Columns(1, 1).Width = 50;
                            xLWorksheet.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                            xLWorksheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                           
                        }

                        workbook.SaveAs(finalPath);

                    }
                    catch (Exception ex2)
                    {
                        _logger.LogError("No codes for catcd " + sectionCode.CodeName);
                    }

                }

            }
            finally
            {
                dataProvider.Close();
            }
        }
        private Tuple<DataTable,DataTable> GetRoleMaskData()
        {
            string sqlCommand = string.Empty;

            sqlCommand = ConfigurationConstants.PHASEI_QueryTemplateForRoleManagement;
          

            DataSet dataSet = dataProvider.ExecuteSelectSql(sqlCommand);
            DataTable RoleMaskData = new DataTable();
            DataTable Organization = new DataTable();
            RoleMaskData = dataSet.Tables[0];
            Organization= dataSet.Tables[1];
            return new Tuple<DataTable, DataTable>(RoleMaskData, Organization);

        }
        private static DataTable GetRolemaskDetails(int domainid, DataTable tbl_rolemaskkeys, DataTable dt2)
        {
            DataTable dt_copy = new DataTable();            
            dt_copy = dt2.Copy();

            var distinctIds = (from row in tbl_rolemaskkeys.AsEnumerable()
                               where row.Field<int>("Role_DomainFK") == domainid
                               select row.Field<int>("Role_PK")).Distinct().ToList();
            for (int i = 4; i < distinctIds.Count + 4; i++)
            {
                int rolemask_id = Int32.Parse(distinctIds[i - 4].ToString());

                var RoleName = (from row in tbl_rolemaskkeys.AsEnumerable()
                                where row.Field<int>("Role_PK") == rolemask_id
                                select row.Field<string>("Role_Name")).Distinct().ToList();

                var rolemaskkeys = from iwcroleperm in tbl_rolemaskkeys.AsEnumerable()
                                   where iwcroleperm.Field<int>("Role_DomainFK") == domainid && iwcroleperm.Field<int>("Role_PK") == rolemask_id
                                   select iwcroleperm;

                dt_copy.Columns.Add(RoleName[0].ToString(), typeof(String));
                dt_copy.AsEnumerable()
              .Join(rolemaskkeys.AsEnumerable(),
                      dt1_Row => dt1_Row.ItemArray[1],
                      dt2_Row => dt2_Row.ItemArray[1],
                      (dt1_Row, dt2_Row) => new { dt1_Row, dt2_Row })
              .ToList()
              .ForEach(o =>
                      o.dt1_Row.SetField(i, o.dt2_Row.ItemArray[2].ToString()));
            }
            dt_copy.DefaultView.Sort = "Sequence";
            dt_copy = dt_copy.DefaultView.ToTable();
            dt_copy.AcceptChanges();

            string intMaxvalue = int.MaxValue.ToString();
            for (int j = 0; j < dt_copy.Rows.Count; j++) // search whole table
            {
                for (int i = 4; i < dt_copy.Columns.Count; i++)
                {
                    Int64 value = 0;
                    if (dt_copy.Rows[j][0].ToString() != dt_copy.Rows[j][1].ToString())
                    {
                        if (dt_copy.Rows[j][2].ToString() != "" && dt_copy.Rows[j][i].ToString() != "")
                        {
                            //if(long.Parse(dt2.Rows[j][2].ToString()) > long.Parse(intMaxvalue) && long.Parse(dt2.Rows[j][i].ToString()) > long.Parse(intMaxvalue))
                            //     dt2.Rows[j][i] = Int64.Parse(dt2.Rows[j][2].ToString()) & Int64.Parse(dt2.Rows[j][i].ToString());
                            //else

                            value = Int64.Parse(dt_copy.Rows[j][2].ToString()) & Int64.Parse(dt_copy.Rows[j][i].ToString());

                            if (Convert.ToUInt64(value) > UInt64.Parse("0"))
                                value = 1;
                            else
                                value = 0;
                        }
                        if (value == 0)
                        {
                            dt_copy.Rows[j][i] = "No";
                        }
                        else if (value == 1)
                        {
                            dt_copy.Rows[j][i] = "Yes";
                        }
                        else
                        {
                            dt_copy.Rows[j][i] = dt_copy.Rows[j][i];
                        }
                    }
                    else
                    {

                        dt_copy.Rows[j][i] = "";
                    }



                }
            }
            int totalColumns = dt_copy.Columns.Count;

            return dt_copy;
        }
        private DataTable PullSectionSettingvaluesIntoTemp(CodeDefinitionDTO securityCode, List<SectionTemplate> Section)
        {
            string sqlCommand = string.Empty;
            sqlCommand = ConfigurationConstants.PHASEI_QueryTemplateForRoleManagement;
            DataSet dataSet = dataProvider.ExecuteSelectSql(sqlCommand);
            //DataTable OrgHierarchy = new DataTable();
            DataTable roleManagement = new DataTable();
            roleManagement = dataSet.Tables[0];
            DataTable dt = new DataTable();
            dt.Columns.Add("SettinName");
            dt.Columns.Add("DecimalValue");

           
            return roleManagement;
        }
    }
}

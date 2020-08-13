using Sumtotal.ConfigurationsAutomation.Utilities;
using SumTotal.Framework.Container;
using SumTotal.Framework.Data;
using SumTotal.Services.Jobs.Contracts;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class Import : BaseExtract
    {
        ServiceJobContext jobContext = new ServiceJobContext();


        public Import()
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

    }
}

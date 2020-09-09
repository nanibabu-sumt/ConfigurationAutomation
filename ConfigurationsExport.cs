using Sumtotal.ConfigurationsAutomation.Contracts;
using SumTotal.Framework.Container;
using SumTotal.Framework.Core.Contracts.Logging;
using SumTotal.Framework.Core.MultiTenancy;
using SumTotal.Services.DataContracts.Core.Lookups;
using SumTotal.Services.Facade.Contracts.Core.Lookups;
using SumTotal.Services.Jobs.Contracts;
using SumTotal.Services.Jobs.JobProcessors;
using System;
using System.Collections.Generic;

namespace Sumtotal.ConfigurationsAutomation
{
    public class ConfigurationsExport : BaseCustomJob
    {
        ServiceJobContext jobContext = new ServiceJobContext();

        bool disposed = false;
        IBaseExtract baseExtract;
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            string configurationCode = string.Empty;
            configurationCode = Convert.ToString(parameters["ConfigCode"]);
            int configCode = 0;            
            bool intResultTryParse = int.TryParse(configurationCode, out configCode);
            if (string.IsNullOrEmpty(configurationCode) || intResultTryParse == false)
            {
                _logger.LogError("Configure System Job Properly Missing Config Code or Invalid Input.");
                return;
            }
            ICodeDefinitionFacade facade = SumtContainer.Resolve<ICodeDefinitionFacade>();
            CodeDefinitionDTO codeDefinition = facade.GetCodeDefinitionWithAttributes(configCode);

            if(codeDefinition == null)
            {
                _logger.LogError("Invalid Category Code : " + configCode);
                return;
            }
            if (codeDefinition.Codes.Count == 0)
            {
                _logger.LogError("Configure Category Code properly for " + configCode);
                return;
            }

            if (codeDefinition.Codes[0].CodeAttributeDTO != null)
            {
                //Attr1Value == Groups to be processed.
                //Attr2Value == Master Category Code.
                //Attr3Value == Domains to be consider.
                ConfigurationParameters configurationParameters = new ConfigurationParameters();
                configurationParameters.MasterCategoryCode = String.IsNullOrEmpty(codeDefinition.Codes[0].CodeAttributeDTO.Attr2Val) ? 0 : Convert.ToInt32(codeDefinition.Codes[0].CodeAttributeDTO.Attr2Val);
                configurationParameters.Domains = String.IsNullOrEmpty(codeDefinition.Codes[0].CodeAttributeDTO.Attr3Val)? null : codeDefinition.Codes[0].CodeAttributeDTO.Attr3Val.Split();
                bool processExport = Boolean.TryParse(codeDefinition.Codes[0].CodeAttributeDTO.Attr4Val, out processExport);
                bool processExtract = Boolean.TryParse(codeDefinition.Codes[0].CodeAttributeDTO.Attr5Val, out processExtract);
                bool processImport = Boolean.TryParse(codeDefinition.Codes[0].CodeAttributeDTO.Attr6Val, out processImport);
                configurationParameters.ProcessExport = processExport;
                configurationParameters.ProcessExtract = processExtract;
                configurationParameters.ProcessImport = processImport;
                string group;
                group = Convert.ToString(codeDefinition.Codes[0].CodeAttributeDTO.Attr1Val);
                string[] groups = group.Split(',');
                if (groups.Length > 0)
                {
                    foreach (var g in groups)
                    {
                        baseExtract = SettingsFactory.GetExtractor(g);
                        configurationParameters.Group = g;
                        baseExtract.LoadDependencies(_logger,configurationParameters);
                        if(configurationParameters.ProcessExtract)
                            baseExtract.Execute(context, parameters);
                        if(configurationParameters.ProcessExport)
                            baseExtract.ExecuteExport(context, parameters);
                        if (configurationParameters.ProcessImport)
                            baseExtract.ExecuteImport(context, parameters);
                    }
                }
                else
                {
                    _logger.LogError("Configure Groups to be processed properly.");
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            try
            {
                if (disposed)
                    return;

                if (disposing)
                {
                    if (jobContext != null)
                    {
                        //Call IDisposable on any that implements the interface
                        foreach (KeyValuePair<string, object> kvp in jobContext.JobParameters)
                        {
                            IDisposable iDisposable = kvp.Value as IDisposable;
                            if (iDisposable != null)
                                iDisposable.Dispose();
                        }
                    }

                    jobContext = null;
                }
            }
            finally
            {
                disposed = true;
            }
        }
        public ConfigurationsExport(ITenant tenant, ILogger logger) : base(tenant, logger)
        {

        }
    }
}

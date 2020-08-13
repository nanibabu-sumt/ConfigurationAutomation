using Sumtotal.ConfigurationsAutomation.Contracts;
using SumTotal.Framework.Core.Contracts.Logging;
using SumTotal.Framework.Core.MultiTenancy;
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
            string group;
            group = parameters["Group"].ToString();
            group = "II";
            baseExtract = SettingsFactory.GetExtractor(group);
            baseExtract.LoadDependencies(_logger);
            baseExtract.Execute(context, parameters);
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

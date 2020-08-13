using Sumtotal.ConfigurationsAutomation.Contracts;
using SumTotal.Framework.Core.Contracts.Logging;
using SumTotal.Framework.Data;
using SumTotal.Services.Jobs.Contracts;
using System.Collections.Generic;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class BaseExtract : IBaseExtract
    {
        public IDataProvider dataProvider;
        public IBaseExtract baseExtract;
        public ILogger _logger;
        public BaseExtract()
        {

        }

        public virtual void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            return;
        }
        public void LoadDependencies(ILogger logger)
        {
            _logger = logger;
        }
    }
}

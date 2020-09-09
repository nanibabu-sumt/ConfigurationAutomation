﻿using SumTotal.Framework.Core.Contracts.Logging;
using SumTotal.Services.DataContracts.Core.Lookups;
using SumTotal.Services.Jobs.Contracts;
using System.Collections.Generic;

namespace Sumtotal.ConfigurationsAutomation.Contracts
{
    public interface IBaseExtract
    {
        void Execute(ServiceJobContext context, IDictionary<string, object> parameters);
        void LoadDependencies(ILogger logger, ConfigurationParameters configurationParameters);
        void ExecuteExport(ServiceJobContext context, IDictionary<string, object> parameters);
        void ExecuteImport(ServiceJobContext context, IDictionary<string, object> parameters);
        
    }
}

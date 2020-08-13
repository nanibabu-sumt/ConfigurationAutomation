using Sumtotal.ConfigurationsAutomation.Contracts;
using SumTotal.Services.Jobs.Contracts;
using System;
using System.Collections.Generic;

namespace Sumtotal.ConfigurationsAutomation.Services
{
    public class ExtractPhase_III : BaseExtract, IExtractPhase_III
    {
        public void Execute(ServiceJobContext context, IDictionary<string, object> parameters)
        {
            throw new NotImplementedException();
        }
    }
}

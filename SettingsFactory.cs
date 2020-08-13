using Sumtotal.ConfigurationsAutomation.Contracts;
using Sumtotal.ConfigurationsAutomation.Services;

namespace Sumtotal.ConfigurationsAutomation
{
    public static class SettingsFactory
    {
        public static IBaseExtract GetExtractor(string phase)
        {
            switch (phase)
            {
                case "I": return new ExtractPhase_I();
                case "II": return new ExtractPhase_II();
                case "III": return new ExtractPhase_III();
                case "IV": return new Export();
                case "V": return new Import();
                default: return new BaseExtract();
            }
        }
    }
}

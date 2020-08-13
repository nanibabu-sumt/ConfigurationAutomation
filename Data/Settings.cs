namespace Sumtotal.ConfigurationsAutomation.Data
{
    public class Settings
    {
        private static Settings settings = null;
        private Settings() { }
        public static Settings getInstance()
        {
            if (settings == null)
            {
                settings = new Settings();
            }
            return settings;
        }
    }
}

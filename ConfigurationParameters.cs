using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sumtotal.ConfigurationsAutomation
{
    public class ConfigurationParameters
    {
        public int MasterCategoryCode { get; set; }
        public string[] Domains { get; set; }
        public bool ProcessExport { get; set; }
        public bool ProcessExtract { get; set; }
        public bool ProcessImport { get; set; }

        public string Group { get; set; }
    }
}

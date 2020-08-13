using System;

namespace Sumtotal.ConfigurationsAutomation.Data
{
    public class SectionTemplate
    {
        public string SettingKey { get; set; }
        public string SettingName { get; set; }
        public int Sequence { get; set; }
        public bool HasLookup { get; set; }
        public String IsPerson { get; set; }
        public bool IsRule { get; set; }
        public int MergeColumns { get; set; }

        public string LookupData { get; set; }

        public string CodeLookupCatcd { get; set; }

        public string Itmtxt { get; set; }
        //  public int SettingCount { get; set; }
        //private Dictionary<String, String> lookupData;
        //public String SetLookupData
        //{

        //    set
        //    {
        //        lookupData = new Dictionary<String, string>();
        //        var list = value.ToString().Split(';').ToList();
        //        if (list.Count == 0) return;
        //        list.ForEach(l =>
        //        {
        //            var item = l.Split(':');
        //            if (item.Length > 1)
        //                lookupData.Add(item[0], item[1]);
        //        });
        //    }
        //}
        //public Dictionary<String, String> GetLookupData
        //{
        //    get { return lookupData; }
        //}
    }
}

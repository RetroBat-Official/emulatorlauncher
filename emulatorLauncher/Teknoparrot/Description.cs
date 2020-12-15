using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TeknoParrotUi.Common
{
    public enum GPUSTATUS
    {
        NO_INFO,
        // no support at all
        NO,
        // runs fine
        OK,
        // requires fix from Discord
        WITH_FIX,
        // runs but with issues
        HAS_ISSUES
    }

    public class Description
    {
        public string platform;
        public string release_year;
   //     [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS nvidia;
        public string nvidia_issues;
    //    [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS amd;
        public string amd_issues;
     //   [JsonConverter(typeof(StringEnumConverter))]
        public GPUSTATUS intel;
        public string intel_issues;
        public string general_issues;

    }
}

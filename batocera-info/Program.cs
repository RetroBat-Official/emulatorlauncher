using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Management;

namespace batocera_info
{
    class Program
    {    
        static SysInfo[] systemInfo = 
        {
            new SysInfo("Win32_Processor", "NAME", "CPU"),
            new SysInfo("Win32_Processor", "NUMBEROFLOGICALPROCESSORS", "CORES"),
        };

        static void Main(string[] args)
        {
            foreach (var si in systemInfo.GroupBy(s => s.Class))
            {
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("select * from " + si.Key);
                foreach (ManagementObject share in searcher.Get())
                {
                    foreach (PropertyData pd in share.Properties)
                    {
                        var name = pd.Name.ToUpper();

                        var info = si.FirstOrDefault(v => v.Name == name);
                        if (info != null)
                        {
                            Console.WriteLine(info.OutName + ": " + (pd.Value == null ? "" : pd.Value.ToString()));
                        }                        
                    }
                }
            }
        }
    }

    class SysInfo
    {
        public SysInfo(string cls, string name, string outname)
        {
            Class = cls;
            Name = name;
            OutName = outname;
        }

        public string Class { get; set; }
        public string Name { get; set; }
        public string OutName { get; set; }
    }

}

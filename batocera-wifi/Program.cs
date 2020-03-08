using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NativeWifi;

namespace batocera_wifi
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "enable")
            {
                return;
            }

            if (args.Length > 0 && args[0] == "disable")
            {
                return;
            }

            if (args.Length == 0 || args[0] == "list" || args[0] == "scanlist")
                doList();            
        }

        private static void doList()
        {
            List<string> list = new List<string>();

            WlanClient client = new WlanClient();            
            foreach (WlanClient.WlanInterface wlanIface in client.Interfaces)
            {                                
                // Lists all networks with WEP security
                Wlan.WlanAvailableNetwork[] networks = wlanIface.GetAvailableNetworkList(0);
                foreach (Wlan.WlanAvailableNetwork network in networks)
                {
                    Wlan.Dot11Ssid ssid = network.dot11Ssid;
                    string networkname = Encoding.ASCII.GetString(ssid.SSID, 0, (int)ssid.SSIDLength);
                    if (!string.IsNullOrEmpty(networkname))
                        list.Add(networkname.ToString());
                }
            }

            foreach (var s in list.OrderBy(l => l))
                Console.WriteLine(s);
        }
    }
}

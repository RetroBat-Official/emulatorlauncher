using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InTheHand.Net.Bluetooth.Win32;

namespace batocera_bluetooth
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "trust")
                return;

            if (args.Length > 0 && args[0] == "remove")
                return;

            if (args.Length > 0 && args[0] == "save")
                return;

            if (args.Length > 0 && args[0] == "restore")
                return;

            // "list" command
            if (args.Length == 0 || args[0] == "list" || args[0] == "scanlist")
                GetPairedDevices();
        }

        static void GetPairedDevices()
        {
            BLUETOOTH_DEVICE_SEARCH_PARAMS search = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            search.cTimeoutMultiplier = 8;
            search.fReturnAuthenticated = true;
            search.fReturnRemembered = false;
            search.fReturnUnknown = false;
            search.fReturnConnected = false;
            search.fIssueInquiry = false;

            BLUETOOTH_DEVICE_INFO device = BLUETOOTH_DEVICE_INFO.Create();
            IntPtr searchHandle = NativeMethods.BluetoothFindFirstDevice(ref search, ref device);
            if (searchHandle != IntPtr.Zero)
            {                
                Console.WriteLine(ToMacAdress(device.Address)+ " " + device.szName);

                while (NativeMethods.BluetoothFindNextDevice(searchHandle, ref device))
                    Console.WriteLine(ToMacAdress(device.Address) + " " + device.szName);

                NativeMethods.BluetoothFindDeviceClose(searchHandle);
            }
        }

        private static string ToMacAdress(ulong adress)
        {
            const string separator = ":";

            byte[] data = BitConverter.GetBytes(adress);

            System.Text.StringBuilder result = new System.Text.StringBuilder(18);
            result.Append(data[5].ToString("X2") + separator);
            result.Append(data[4].ToString("X2") + separator);
            result.Append(data[3].ToString("X2") + separator);
            result.Append(data[2].ToString("X2") + separator);
            result.Append(data[1].ToString("X2") + separator);
            result.Append(data[0].ToString("X2"));
            return result.ToString();
        }
    }
}

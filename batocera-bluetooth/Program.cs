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

            if (args.Length == 0 || args[0] == "live_devices")
            {
                GetPairedDevices();
                return;
            }

            // "list" command
            if (args.Length == 0 || args[0] == "list" || args[0] == "scanlist")
                GetPairedDevices();
        }

        /*
        [root@BATOCERA /userdata/system]# batocera-bluetooth live_devices
        +E4:17:D8:5D:8F:78 8Bitdo SF30 Pro
        +E4:17:D8:0C:0B:70 8BitDo N30 Pro 2
        +14:F6:D8:66:7B:5E setsouko
                */

        static void GetPairedDevices()
        {
            BLUETOOTH_DEVICE_SEARCH_PARAMS search = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            search.cTimeoutMultiplier = 10;
            search.fReturnAuthenticated = true;
            search.fReturnRemembered = false;
            search.fReturnUnknown = true;
            search.fReturnConnected = true;
            search.fIssueInquiry = true;

            BLUETOOTH_DEVICE_INFO device = BLUETOOTH_DEVICE_INFO.Create();

            IntPtr searchHandle = NativeMethods.BluetoothFindFirstDevice(ref search, ref device);            
            if (searchHandle != IntPtr.Zero)
            { 
                while (true)
                {
                    bool isAudioVideo = (device.ulClassofDevice & 0x400) == 0x400;
                    bool isPeripheral = (device.ulClassofDevice & 0x500) == 0x500;
                    bool isKeyboard = (device.ulClassofDevice & 0x540) == 0x540;
                    bool isMouse = (device.ulClassofDevice & 0x580) == 0x580;
                    bool isJoystick = (device.ulClassofDevice & 0x504) == 0x504;
                    bool isGamepad = (device.ulClassofDevice & 0x508) == 0x508;

                    if (isPeripheral)
                    {
                        Console.WriteLine(ToMacAdress(device.Address) + " " + device.szName);
                    }

                    if (!NativeMethods.BluetoothFindNextDevice(searchHandle, ref device))
                        break;
                }

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

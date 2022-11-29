using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InTheHand.Net.Bluetooth.Win32;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.IO;

namespace batocera_bluetooth
{
    class Program
    {
        // https://github.com/inthehand/32feet/blob/main/InTheHand.Net.Bluetooth/Platforms/Win32/BluetoothSecurity.win32.cs

        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "trust" && args.Length == 1)
            {
                GetLiveDevices(dev =>
                    {
                        if (dev.Type != "joystick")
                            return false;

                        PerformPairRequest(dev.Address);
                        return true;
                    });

                return;
            }

            if (args.Length > 0 && args[0] == "trust" && args.Length > 1)
            {
                var mc = FromMacAdress(args[1]);
                if (mc != 0)                    
                    PerformPairRequest(mc);

                return;
            }

            if (args.Length > 1 && args[0] == "remove")
            {
                var mc = FromMacAdress(args[1]);
                if (mc != 0)
                {
                    var addr = new BLUETOOTH_ADDRESS();
                    addr.ullLong = mc;
                    NativeMethods.BluetoothRemoveDevice(ref addr);
                }
                return;
            }

            if (args.Length > 0 && args[0] == "save")
                return;

            if (args.Length > 0 && args[0] == "restore")
                return;

            if (args[0] == "stop_live_devices")
            {
                var currentProcess = Process.GetCurrentProcess();
                foreach (var px in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location)))
                {
                    try
                    {
                        if (px.Id != currentProcess.Id)
                            px.Kill();
                    }
                    catch { }
                }            
            }

            if (args[0] == "live_devices")
            {
                GetLiveDevices();                
                return;
            }

            // "list" command
            if (args.Length == 0 || args[0] == "list" || args[0] == "scanlist")
                GetPairedDevices();
        }

        static void PerformPairRequest(ulong device)
        {
            BLUETOOTH_DEVICE_INFO info = new BLUETOOTH_DEVICE_INFO();
            info.dwSize = Marshal.SizeOf(info);
            info.Address = device; 
            
            NativeMethods.BluetoothGetDeviceInfo(IntPtr.Zero, ref info);
            // don't wait on this process if already paired
            if (info.fAuthenticated)
                return;

            var callback = new NativeMethods.BluetoothAuthenticationCallbackEx(Callback);

            IntPtr handle = IntPtr.Zero;
            int result = NativeMethods.BluetoothRegisterForAuthenticationEx(ref info, out handle, callback, IntPtr.Zero);

            if (NativeMethods.BluetoothAuthenticateDeviceEx(IntPtr.Zero, IntPtr.Zero, ref info, null, BluetoothAuthenticationRequirements.MITMProtectionNotRequired)==0)
                _waitHandle.WaitOne();

            NativeMethods.BluetoothUnregisterAuthentication(handle);
        }

        static EventWaitHandle _waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        private static bool Callback(IntPtr pvParam, ref BLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS pAuthCallbackParams)
        {
            try
            {
                switch (pAuthCallbackParams.authenticationMethod)
                {
                    case BluetoothAuthenticationMethod.Passkey:
                    case BluetoothAuthenticationMethod.NumericComparison:
                        var nresponse = new BLUETOOTH_AUTHENTICATE_RESPONSE__NUMERIC_COMPARISON_PASSKEY_INFO
                        {
                            authMethod = pAuthCallbackParams.authenticationMethod,
                            bthAddressRemote = pAuthCallbackParams.deviceInfo.Address,
                            numericComp_passkey = pAuthCallbackParams.Numeric_Value_Passkey
                        };

                        int result = NativeMethods.BluetoothSendAuthenticationResponseEx(IntPtr.Zero, ref nresponse);

                        return result == 0;

                    case BluetoothAuthenticationMethod.Legacy:
                        break;
                }
            }
            catch(Exception ex)
            { 
            }
            finally
            {
                _waitHandle.Set();
            }

            return false;
        }

        class BluetoothDevice
        {
            public BluetoothDevice(BLUETOOTH_DEVICE_INFO dev)
            {
                Address = dev.Address;
                Name = dev.szName;
                ClassOfDevice = dev.ulClassofDevice;
            }

            public string Type
            {
                get
                {
                    if ((ClassOfDevice & 0x504) == 0x504 || (ClassOfDevice & 0x508) == 0x508)
                        return "joystick";

                    if ((ClassOfDevice & 0x540) == 0x540)
                        return "keyboard";

                    if ((ClassOfDevice & 0x580) == 0x580)
                        return "mouse";

                    if ((ClassOfDevice & 0x400) == 0x400)
                        return "audio";

                    return "";
                }
            }

            public ulong Address { get; set; }
            public string Name { get; set; }
            public ulong ClassOfDevice { get; set; }

            public override string ToString()
            {
                return ToMacAdress(Address) + " " + Name;
            }
        }

        static void GetLiveDevices(Func<BluetoothDevice, bool> deviceFoundEvent = null)
        {
            int time = Environment.TickCount;

            BLUETOOTH_DEVICE_SEARCH_PARAMS search = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            search.cTimeoutMultiplier = 1;
            search.fReturnAuthenticated = false;
            search.fReturnRemembered = false;
            search.fReturnUnknown = true;
            search.fReturnConnected = true;
            search.fIssueInquiry = true;

            List<BluetoothDevice> connectedDevices = null;

            while (true)
            {
                var devices = new List<BluetoothDevice>();

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

                        if (isPeripheral || isAudioVideo)
                            devices.Add(new BluetoothDevice(device));
                        
                        if (!NativeMethods.BluetoothFindNextDevice(searchHandle, ref device))
                            break;
                    }

                    NativeMethods.BluetoothFindDeviceClose(searchHandle);
                }

                if (deviceFoundEvent == null && connectedDevices != null)
                {
                    foreach (var dev in connectedDevices)
                    {
                        if (!devices.Any(e => e.Address == dev.Address))
                            Console.WriteLine("<device id=\"" + ToMacAdress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\" status=\"removed\"/>");
                    }
                }

                foreach (var dev in devices)
                {
                    if (connectedDevices == null || !connectedDevices.Any(e => e.Address == dev.Address))
                    {
                        if (deviceFoundEvent != null)
                        {
                            if (dev.Type == "joystick")
                            {
                                Console.WriteLine("Found \"" + ToMacAdress(dev.Address) + " " + dev.Name + "\"");
                                if (deviceFoundEvent(dev))
                                    return;
                            }
                        }
                        else
                            Console.WriteLine("<device id=\"" + ToMacAdress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\" status=\"added\"/>");
                    }
                }

                if (deviceFoundEvent != null && Environment.TickCount - time > 60 * 1000)
                    break;

                search.cTimeoutMultiplier = (byte) Math.Min(8, search.cTimeoutMultiplier + 2);
                connectedDevices = devices;
            }
        }

        static void GetPairedDevices()
        {
            BLUETOOTH_DEVICE_SEARCH_PARAMS search = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            search.cTimeoutMultiplier = 8;
            search.fReturnAuthenticated = false;
            search.fReturnRemembered = true;
            search.fReturnUnknown = false;
            search.fReturnConnected = false;
            search.fIssueInquiry = false;

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
                        var dev = new BluetoothDevice(device);
                        Console.WriteLine("<device id=\"" + ToMacAdress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\"/>");

                   //     Console.Write(ToMacAdress(device.Address) + " " + device.szName + "\n");
                    }

                    if (!NativeMethods.BluetoothFindNextDevice(searchHandle, ref device))
                        break;
                }

                NativeMethods.BluetoothFindDeviceClose(searchHandle);
            }
        }
        
        private static ulong FromMacAdress(string adress)
        {
            adress = adress.Replace(":", "");

            ulong value = 0;
            if (ulong.TryParse(adress, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                return value;

            return 0;
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

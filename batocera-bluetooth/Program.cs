using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.IO;
using InTheHand.Net.Bluetooth.Win32;

namespace batocera_bluetooth
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "forgetBT")
            {
                ForgetBluetoothControllers();
                return;
            }

            if (args.Length > 0 && args[0] == "trust" && (args.Length == 1 || args.Length > 1 && args[1] == "input"))
            {
                AutoDetectBluetoothControllers();
                return;
            }

            if (args[0] == "connect" && args.Length > 1)
            {
                ConnectBluetoothController(args[1]);
                return;
            }

            if (args.Length > 0 && args[0] == "trust" && args.Length > 1)
            {
                TrustBluetoothDevice(args[1]);
                return;
            }

            if (args.Length > 1 && args[0] == "remove")
            {
                RemoveBluetoothDevice(args[1]);
                return;
            }

            if (args[0] == "live_devices")
            {
                GetLiveDevices();                
                return;
            }

            if (args[0] == "stop_live_devices")
            {
                StopLiveDevices();
                return;
            }

            if (args.Length > 0 && args[0] == "save")
                return;

            if (args.Length > 0 && args[0] == "restore")
                return;

            // "list" command
            if (args.Length == 0 || args[0] == "list" || args[0] == "scanlist")
                GetPairedDevices();
        }

        private static void TrustBluetoothDevice(string macAddress)
        {
            var mc = FromMacAddress(macAddress);
            if (mc != 0)
                PerformPairRequest(mc);
        }

        private static void RemoveBluetoothDevice(string macAddress)
        {
            var mc = FromMacAddress(macAddress);
            if (mc == 0)
                return;
            
            var addr = new BLUETOOTH_ADDRESS();
            addr.ullLong = mc;
            NativeMethods.BluetoothRemoveDevice(ref addr);            
        }

        private static void StopLiveDevices()
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

        private static void AutoDetectBluetoothControllers()
        {
            bool _isNewMutex;
            Mutex mutex = new Mutex(true, "BlueTooth-GetLiveDevices", out _isNewMutex);

            if (_isNewMutex)
                GetLiveDevices(dev => { PerformPairRequest(dev.Address); return true; });

            if (mutex != null)
            {
                mutex.Close();
                mutex.Dispose();
                mutex = null;
            }
        }

        private static void ConnectBluetoothController(string macAddress)
        {
            var address = FromMacAddress(macAddress);
            if (address == 0)
                return;

            BLUETOOTH_DEVICE_INFO info = new BLUETOOTH_DEVICE_INFO();
            info.dwSize = Marshal.SizeOf(info);
            info.Address = address;

            NativeMethods.BluetoothGetDeviceInfo(IntPtr.Zero, ref info);

            if (info.fAuthenticated)
                ConnectDeviceServices(ref info);
        }

        private static void ForgetBluetoothControllers()
        {
            foreach (var cd in GetPairedDevices(false))
            {
                var addr = new BLUETOOTH_ADDRESS();
                addr.ullLong = cd.Address;
                NativeMethods.BluetoothRemoveDevice(ref addr);
            }
        }

        private static void PerformPairRequest(ulong device)
        {
            BLUETOOTH_DEVICE_INFO info = new BLUETOOTH_DEVICE_INFO();
            info.dwSize = Marshal.SizeOf(info);
            info.Address = device; 
            
            NativeMethods.BluetoothGetDeviceInfo(IntPtr.Zero, ref info);

            // don't wait on this process if already paired
            if (info.fAuthenticated)
            {
                ConnectDeviceServices(ref info);
                return;
            }

            var callback = new NativeMethods.BluetoothAuthenticationCallbackEx(Callback);

            IntPtr handle = IntPtr.Zero;
            int result = NativeMethods.BluetoothRegisterForAuthenticationEx(ref info, out handle, callback, IntPtr.Zero);

            if (NativeMethods.BluetoothAuthenticateDeviceEx(IntPtr.Zero, IntPtr.Zero, ref info, null, BluetoothAuthenticationRequirements.MITMProtectionNotRequired) == 0)
            {
                _waitHandle.WaitOne();
                ConnectDeviceServices(ref info);
            }
            
            NativeMethods.BluetoothUnregisterAuthentication(handle);
        }

        private static bool ConnectDeviceServices(ref BLUETOOTH_DEVICE_INFO deviceInfo)
        {
            var GUID_HID = new Guid("00001124-0000-1000-8000-00805f9b34fb");
            return NativeMethods.BluetoothSetServiceState(IntPtr.Zero, ref deviceInfo, ref GUID_HID, 0x00000001) == 0; // BLUETOOTH_SERVICE_ENABLE
        }

        private static EventWaitHandle _waitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

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
            catch
            {
            }
            finally
            {
                _waitHandle.Set();
            }

            return false;
        }

        static void GetLiveDevices(Func<BluetoothDevice, bool> deviceFoundEvent = null)
        {
            int time = Environment.TickCount;

            BLUETOOTH_DEVICE_SEARCH_PARAMS search = BLUETOOTH_DEVICE_SEARCH_PARAMS.Create();
            search.cTimeoutMultiplier = 1;
            search.fReturnAuthenticated = false;
            search.fReturnRemembered = false;
            search.fReturnUnknown = true;
            search.fReturnConnected = false;
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
                        if (device.IsGamePad)
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
                            Console.WriteLine("<device id=\"" + ToMacAddress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\" status=\"removed\"/>");
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
                                Console.WriteLine("Found \"" + dev.Name + ", " + ToMacAddress(dev.Address) + "\"");
                                if (deviceFoundEvent(dev))
                                    return;
                            }
                        }
                        else
                            Console.WriteLine("<device id=\"" + ToMacAddress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\" status=\"added\"/>");
                    }
                }

                if (deviceFoundEvent != null && Environment.TickCount - time > 60 * 1000) // 1 minute
                    break;

                search.cTimeoutMultiplier = (byte) Math.Min(4, search.cTimeoutMultiplier + 2);
                connectedDevices = devices;
            }
        }

        private static BluetoothDevice[] GetPairedDevices(bool outputToConsole = true)
        {
            var ret = new List<BluetoothDevice>();

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
                    if (device.IsGamePad)
                    {
                        var dev = new BluetoothDevice(device);

                        if (outputToConsole)
                            Console.WriteLine("<device id=\"" + ToMacAddress(dev.Address) + "\" name=\"" + dev.Name + "\" type=\"" + dev.Type + "\"/>");

                        ret.Add(dev);
                    }

                    if (!NativeMethods.BluetoothFindNextDevice(searchHandle, ref device))
                        break;
                }

                NativeMethods.BluetoothFindDeviceClose(searchHandle);
            }

            return ret.ToArray();
        }

        private static ulong FromMacAddress(string adress)
        {
            if (!string.IsNullOrEmpty(adress))
            {
                adress = adress.Replace(":", "");

                ulong value = 0;
                if (ulong.TryParse(adress, System.Globalization.NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
                    return value;
            }

            return 0;
        }
        
        private static string ToMacAddress(ulong adress)
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
                return ToMacAddress(Address) + " " + Name;
            }
        }
    }
}

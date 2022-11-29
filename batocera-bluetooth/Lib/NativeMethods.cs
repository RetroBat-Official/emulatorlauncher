// 32feet.NET - Personal Area Networking for .NET
//
// InTheHand.Net.Bluetooth.Win32.NativeMethods
// 
// Copyright (c) 2003-2020 In The Hand Ltd, All rights reserved.
// This source code is licensed under the MIT License

// https://github.com/inthehand/32feet/tree/main/InTheHand.Net.Bluetooth/Platforms/Win32

using System;
using System.Runtime.InteropServices;

namespace InTheHand.Net.Bluetooth.Win32
{
    internal static class NativeMethods
    {
        private const string bthpropsDll = "bthprops.cpl";
        private const string irpropsDll = "Irprops.cpl";
        private const string wsDll = "ws2_32.dll";

        // Discovery
        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindDeviceClose(IntPtr hFind);

        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern int BluetoothRemoveDevice(ref BLUETOOTH_ADDRESS pAddress);

        [DllImport(bthpropsDll, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothAuthenticateDeviceEx(IntPtr hwndParentIn, IntPtr hRadioIn, ref BLUETOOTH_DEVICE_INFO pbtdiInout, byte[] pbtOobData, 
            BluetoothAuthenticationRequirements authenticationRequirement);

        [DllImport(bthpropsDll, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothGetDeviceInfo(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi);

        [return: MarshalAs(UnmanagedType.Bool)]
        internal delegate bool BluetoothAuthenticationCallbackEx(IntPtr pvParam, ref BLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS pAuthCallbackParams);

        // Requires Vista SP2 or later
        [DllImport(bthpropsDll, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothRegisterForAuthenticationEx(ref BLUETOOTH_DEVICE_INFO pbtdi, out IntPtr phRegHandle, BluetoothAuthenticationCallbackEx pfnCallback, IntPtr pvParam);


        [DllImport(bthpropsDll, SetLastError = false, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothSendAuthenticationResponseEx(IntPtr hRadio, ref BLUETOOTH_AUTHENTICATE_RESPONSE__PIN_INFO pauthResponse);

        [DllImport(bthpropsDll, SetLastError = false, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothSendAuthenticationResponseEx(IntPtr hRadio, ref BLUETOOTH_AUTHENTICATE_RESPONSE__NUMERIC_COMPARISON_PASSKEY_INFO pauthResponse);

        [DllImport(bthpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothUnregisterAuthentication(IntPtr hRegHandle);

        /*
    

        [DllImport("User32")]
        internal static extern IntPtr GetActiveWindow();

        [DllImport(bthpropsDll, SetLastError = false, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothSendAuthenticationResponseEx(IntPtr hRadio, ref BLUETOOTH_AUTHENTICATE_RESPONSE__OOB_DATA_INFO pauthResponse);

        [DllImport(bthpropsDll, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int BluetoothAuthenticateDeviceEx(IntPtr hwndParentIn, IntPtr hRadioIn, ref BLUETOOTH_DEVICE_INFO pbtdiInout, byte[] pbtOobData, BluetoothAuthenticationRequirements authenticationRequirement);

        // Radio
        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindNextRadio(IntPtr hFind, out IntPtr phRadio);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothFindRadioClose(IntPtr hFind);
        
        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern int BluetoothGetRadioInfo(IntPtr hRadio, ref BLUETOOTH_RADIO_INFO pRadioInfo);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothIsConnectable(IntPtr hRadio);
        
        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothIsDiscoverable(IntPtr hRadio);


        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothEnableDiscovery(IntPtr hRadio, bool fEnabled);
        
        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothEnableIncomingConnections(IntPtr hRadio, bool fEnabled);

        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern int BluetoothEnumerateInstalledServices(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi, ref int pcServices, byte[] pGuidServices);
        [DllImport(irpropsDll, SetLastError = true)]
        internal static extern int BluetoothSetServiceState(IntPtr hRadio, ref BLUETOOTH_DEVICE_INFO pbtdi, ref Guid pGuidService, uint dwServiceFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(IntPtr handle);

        //SetService
        [DllImport(wsDll, EntryPoint = "WSASetService", SetLastError = true)]
        internal static extern int WSASetService(ref WSAQUERYSET lpqsRegInfo, WSAESETSERVICEOP essoperation, int dwControlFlags);

        // Picker
        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothSelectDevices(ref BLUETOOTH_SELECT_DEVICE_PARAMS pbtsdp);

        [DllImport(irpropsDll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool BluetoothSelectDevicesFree(ref BLUETOOTH_SELECT_DEVICE_PARAMS pbtsdp);

        internal delegate bool PFN_DEVICE_CALLBACK(IntPtr pvParam, ref BLUETOOTH_DEVICE_INFO pDevice);
    }

    /// <summary>
    /// The BLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS structure contains specific configuration information about the Bluetooth device responding to an authentication request.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct BLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS
    {
        /// <summary>
        /// A BLUETOOTH_DEVICE_INFO structure that contains information about a Bluetooth device.
        /// </summary>
        internal BLUETOOTH_DEVICE_INFO deviceInfo;

        /// <summary>
        /// A BLUETOOTH_AUTHENTICATION_METHOD enumeration that defines the authentication method utilized by the Bluetooth device.
        /// </summary>
        internal BluetoothAuthenticationMethod authenticationMethod;

        /// <summary>
        /// A BLUETOOTH_IO_CAPABILITY enumeration that defines the input/output capabilities of the Bluetooth device.
        /// </summary>
        internal BluetoothIoCapability ioCapability;

        /// <summary>
        /// A AUTHENTICATION_REQUIREMENTS specifies the 'Man in the Middle' protection required for authentication.
        /// </summary>
        internal BluetoothAuthenticationRequirements authenticationRequirements;

        //union{
        //    ULONG   Numeric_Value;
        //    ULONG   Passkey;
        //};

        /// <summary>
        /// A ULONG value used for Numeric Comparison authentication.
        /// or
        /// A ULONG value used as the passkey used for authentication.
        /// </summary>
        internal uint Numeric_Value_Passkey;
         */
    }

    [StructLayout(LayoutKind.Sequential, Size = 20)]
    internal struct BLUETOOTH_PIN_INFO
    {
        public const int BTH_MAX_PIN_SIZE = 16;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = BTH_MAX_PIN_SIZE)]
        internal byte[] pin;
        internal int pinLength;
    }

    [StructLayout(LayoutKind.Sequential, Size = 52)]
    internal struct BLUETOOTH_AUTHENTICATE_RESPONSE__PIN_INFO // see above
    {
        internal ulong bthAddressRemote;
        internal BluetoothAuthenticationMethod authMethod;
        internal BLUETOOTH_PIN_INFO pinInfo;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 12)]
        private readonly byte[] _padding;
        internal byte negativeResponse;
    }

    internal enum BluetoothAuthenticationMethod : int // MSFT+Win32 BLUETOOTH_AUTHENTICATION_METHOD
    {
        /// <summary>
        /// The Bluetooth device supports authentication via a PIN.
        /// </summary>
        Legacy = 0x1,

        /// <summary>
        /// The Bluetooth device supports authentication via out-of-band data.
        /// </summary>
        OutOfBand,

        /// <summary>
        /// The Bluetooth device supports authentication via numeric comparison.
        /// </summary>
        NumericComparison,

        /// <summary>
        /// The Bluetooth device supports authentication via passkey notification.
        /// </summary>
        PasskeyNotification,

        /// <summary>
        /// The Bluetooth device supports authentication via passkey.
        /// </summary>
        Passkey,
    }

    internal enum BluetoothIoCapability : int // MSFT+Win32 BLUETOOTH_IO_CAPABILITY
    {
        /// <summary>
        /// The Bluetooth device is capable of output via display only.
        /// </summary>
        DisplayOnly = 0x00,

        /// <summary>
        /// The Bluetooth device is capable of output via a display, 
        /// and has the additional capability to presenting a yes/no question to the user.
        /// </summary>
        DisplayYesNo = 0x01,

        /// <summary>
        /// The Bluetooth device is capable of input via keyboard.
        /// </summary>
        KeyboardOnly = 0x02,

        /// <summary>
        /// The Bluetooth device is not capable of input/output.
        /// </summary>
        NoInputNoOutput = 0x03,

        /// <summary>
        /// The input/output capabilities for the Bluetooth device are undefined.
        /// </summary>
        Undefined = 0xff
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BLUETOOTH_AUTHENTICATION_CALLBACK_PARAMS
    {
        /// <summary>
        /// A BLUETOOTH_DEVICE_INFO structure that contains information about a Bluetooth device.
        /// </summary>
        internal BLUETOOTH_DEVICE_INFO deviceInfo;

        /// <summary>
        /// A BLUETOOTH_AUTHENTICATION_METHOD enumeration that defines the authentication method utilized by the Bluetooth device.
        /// </summary>
        internal BluetoothAuthenticationMethod authenticationMethod;

        /// <summary>
        /// A BLUETOOTH_IO_CAPABILITY enumeration that defines the input/output capabilities of the Bluetooth device.
        /// </summary>
        internal BluetoothIoCapability ioCapability;

        /// <summary>
        /// A AUTHENTICATION_REQUIREMENTS specifies the 'Man in the Middle' protection required for authentication.
        /// </summary>
        internal BluetoothAuthenticationRequirements authenticationRequirements;

        //union{
        //    ULONG   Numeric_Value;
        //    ULONG   Passkey;
        //};

        /// <summary>
        /// A ULONG value used for Numeric Comparison authentication.
        /// or
        /// A ULONG value used as the passkey used for authentication.
        /// </summary>
        internal uint Numeric_Value_Passkey;
    }
    enum BluetoothAuthenticationRequirements
    {
        MITMProtectionNotRequired = 0x00,
        MITMProtectionRequired = 0x01,
        MITMProtectionNotRequiredBonding = 0x02,
        MITMProtectionRequiredBonding = 0x03,
        MITMProtectionNotRequiredGeneralBonding = 0x04,
        MITMProtectionRequiredGeneralBonding = 0x05,
        MITMProtectionNotDefined = 0xff
    }

    [StructLayout(LayoutKind.Explicit)]
    struct BLUETOOTH_ADDRESS
    {
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.I8)]
        public ulong ullLong;
        [FieldOffset(0)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_0;
        [FieldOffset(1)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_1;
        [FieldOffset(2)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_2;
        [FieldOffset(3)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_3;
        [FieldOffset(4)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_4;
        [FieldOffset(5)]
        [MarshalAs(UnmanagedType.U1)]
        public Byte rgBytes_5;
    };

    [StructLayout(LayoutKind.Sequential, Size = 52)]
    internal struct BLUETOOTH_AUTHENTICATE_RESPONSE__NUMERIC_COMPARISON_PASSKEY_INFO // see above
    {
        internal ulong bthAddressRemote;
        internal BluetoothAuthenticationMethod authMethod;
        internal uint numericComp_passkey;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 28)]
        private readonly byte[] _padding;

        internal byte negativeResponse;
    }
}
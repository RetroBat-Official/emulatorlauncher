using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;

namespace emulatorLauncher.Tools
{
    public static class SdlJoystickGuidManager
    {
        /*
        This GUID fits the standard form:
             * 16-bit bus
             * 16-bit CRC16 of the joystick name (can be zero)
             * 16-bit vendor ID
             * 16-bit zero
             * 16-bit product ID
             * 16-bit zero
             * 16-bit version
             * 8-bit driver identifier ('h' for HIDAPI, 'x' for XInput, etc.)
             * 8-bit driver-dependent type info`
        */

        public static string ToSdlGuidString(this Guid guid)
        {
            string esGuidString = guid.ToString();

            string ret =
                esGuidString.Substring(6, 2) +
                esGuidString.Substring(4, 2) +
                esGuidString.Substring(2, 2) +
                esGuidString.Substring(0, 2) +
                esGuidString.Substring(10 + 1, 2) +
                esGuidString.Substring(8 + 1, 2) +
                esGuidString.Substring(14 + 2, 2) +
                esGuidString.Substring(12 + 2, 2) +
                esGuidString.Substring(16 + 3, 4) +
                esGuidString.Substring(20 + 4);

            return ret;
        }

        public static System.Guid FromSdlGuidString(this string esGuidString)
        {
            if (esGuidString.Length == 32)
            {
                string guid =
                    esGuidString.Substring(6, 2) +
                    esGuidString.Substring(4, 2) +
                    esGuidString.Substring(2, 2) +
                    esGuidString.Substring(0, 2) +
                    "-" +
                    esGuidString.Substring(10, 2) +
                    esGuidString.Substring(8, 2) +
                    "-" +
                    esGuidString.Substring(14, 2) +
                    esGuidString.Substring(12, 2) +
                    "-" +
                    esGuidString.Substring(16, 4) +
                    "-" +
                    esGuidString.Substring(20);

                try { return new System.Guid(guid); }
                catch { }
            }

            return Guid.Empty;
        }

        public static Guid ConvertSdlGuid(this Guid guid, string name, SdlVersion version)
        {
            if (version == SdlVersion.Current)
                return guid;

            Guid ret = guid;

            if (version == SdlVersion.SDL2_26 && name != null)
            {
                var crc16 = SDL.SDL_Swap16(SDL.SDL_crc16(System.Text.Encoding.UTF8.GetBytes(name ?? ""))).ToString("X4");

                var gg = guid.ToSdlGuidString();
                var ggs = gg.Substring(0, 4) + crc16 + gg.Substring(8);
                ret = ggs.FromSdlGuidString();
            }
            else
            {
                // Pre 2.26x : remove '16-bit CRC16 of the joystick name'
                ret = new Guid("0000" + guid.ToString().Substring(4));
                if (version == SdlVersion.SDL2_24)
                    return ret;
            }

            // SDL 2.0.X : remove 8-bit driver-dependent type info for switch controllers
            if (GetJoystickNameCrc16(guid) == 0xd455) // "HORI Wireless Switch Pad" ??? Must check
                return new Guid(ret.ToString().Substring(0, 34) + "00");

            if (guid.GetVendorID() == VendorId.USB_VENDOR_NINTENDO)
            {
                var prod = guid.GetProductID();

                if (prod == ProductId.USB_PRODUCT_NINTENDO_N64_CONTROLLER ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SEGA_GENESIS_CONTROLLER ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SNES_CONTROLLER ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SWITCH_JOYCON_GRIP ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SWITCH_JOYCON_LEFT ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SWITCH_JOYCON_PAIR ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SWITCH_JOYCON_RIGHT ||
                    prod == ProductId.USB_PRODUCT_NINTENDO_SWITCH_PRO)
                {
                    // Remove 8-bit driver-dependent type info
                    ret = new Guid(ret.ToString().Substring(0, 34) + "00");
                }
            }

            return ret;
        }

        public static SdlVersion GetSdlVersion(string fileName)
        {
            if (!File.Exists(fileName))
                return SdlVersion.Current;

            System.Version version;

            try
            {
                if (!System.Version.TryParse(FileVersionInfo.GetVersionInfo(fileName).ProductVersion.Replace(",", ".").Replace(" ", ""), out version))
                    return SdlVersion.Current;
            }
            catch
            {
                return SdlVersion.Current;
            }

            if (version.Minor > 26)
                return SdlVersion.Current;

            if (version.Minor > 0)
                return SdlVersion.SDL2_24;

            return SdlVersion.SDL2_0_X;
        }

        public static ushort GetJoystickNameCrc16(this Guid guid)
        {
            var sdlGuid = guid.ToSdlGuidString().Substring(0, 4).ToUpper();
            return ushort.Parse(sdlGuid, System.Globalization.NumberStyles.HexNumber);
        }

        public static SdlWrappedTechId GetWrappedTechID(this Guid guid)
        {
            var sdlGuid = guid.ToSdlGuidString();
            string id = sdlGuid.Substring(28, 2).ToUpper();
            int intValue = int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return (SdlWrappedTechId)intValue;
        }

        public static VendorId GetVendorID(this Guid guid)
        {
            var sdlGuid = guid.ToSdlGuidString();
            string id = (sdlGuid.Substring(10, 2) + sdlGuid.Substring(8, 2)).ToUpper();
            int intValue = int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return (VendorId)intValue;
        }

        public static ProductId GetProductID(this Guid guid)
        {
            var sdlGuid = guid.ToSdlGuidString();
            string id = (sdlGuid.Substring(18, 2) + sdlGuid.Substring(16, 2)).ToUpper();
            int intValue = int.Parse(id, System.Globalization.NumberStyles.HexNumber);
            return (ProductId)intValue;
        }

        public static Guid ToXInputGuid(this Guid guid, int playerIndex)
        {
            var sdlGuid = guid.ToSdlGuidString();
            string id = (sdlGuid.Substring(0, 28) + "78" + playerIndex.ToString("X2")).ToUpper();
            return id.FromSdlGuidString();
        }
    }

    public enum SdlVersion
    {
        Current,
        SDL2_24,
        SDL2_26,
        SDL2_0_X
    }

    public enum SdlWrappedTechId
    {
        DirectInput = 0,
        HID = 0x68, // 'h'
        RawInput = 0x72, // 'x'
        Virtual = 0x76, // 'v'
        XInput = 0x78 // 'x'
    }

    public enum VendorId
    {
        UNKNOWN = 0,
        USB_VENDOR_8BITDO = 0x2dc8,
        USB_VENDOR_AMAZON = 0x1949,
        USB_VENDOR_APPLE = 0x05ac,
        USB_VENDOR_DRAGONRISE = 0x0079,
        USB_VENDOR_GOOGLE = 0x18d1,
        USB_VENDOR_HORI = 0x0f0d,
        USB_VENDOR_HYPERKIN = 0x2e24,
        USB_VENDOR_MADCATZ = 0x0738,
        USB_VENDOR_MICROSOFT = 0x045e,
        USB_VENDOR_NACON = 0x146b,
        USB_VENDOR_NINTENDO = 0x057e,
        USB_VENDOR_NVIDIA = 0x0955,
        USB_VENDOR_PDP = 0x0e6f,
        USB_VENDOR_POWERA = 0x24c6,
        USB_VENDOR_POWERA_ALT = 0x20d6,
        USB_VENDOR_QANBA = 0x2c22,
        USB_VENDOR_RAZER = 0x1532,
        USB_VENDOR_SHANWAN = 0x2563,
        USB_VENDOR_SHANWAN_ALT = 0x20bc,
        USB_VENDOR_SONY = 0x054c,
        USB_VENDOR_THRUSTMASTER = 0x044f,
        USB_VENDOR_VALVE = 0x28de,
        USB_VENDOR_ZEROPLUS = 0x0c12
    }

    public enum ProductId
    {
        UNKNOWN = 0,
        USB_PRODUCT_8BITDO_XBOX_CONTROLLER = 0x2002,
        USB_PRODUCT_AMAZON_LUNA_CONTROLLER = 0x0419,
        USB_PRODUCT_GOOGLE_STADIA_CONTROLLER = 0x9400,
        USB_PRODUCT_EVORETRO_GAMECUBE_ADAPTER = 0x1846,
        USB_PRODUCT_HORI_FIGHTING_COMMANDER_OCTA_SERIES_X = 0x0150,
        USB_PRODUCT_HORI_FIGHTING_STICK_ALPHA_PS4 = 0x011c,
        USB_PRODUCT_HORI_FIGHTING_STICK_ALPHA_PS5 = 0x0184,
        USB_PRODUCT_NINTENDO_GAMECUBE_ADAPTER = 0x0337,
        USB_PRODUCT_NINTENDO_N64_CONTROLLER = 0x2019,
        USB_PRODUCT_NINTENDO_SEGA_GENESIS_CONTROLLER = 0x201e,
        USB_PRODUCT_NINTENDO_SNES_CONTROLLER = 0x2017,
        USB_PRODUCT_NINTENDO_SWITCH_JOYCON_GRIP = 0x200e,
        USB_PRODUCT_NINTENDO_SWITCH_JOYCON_LEFT = 0x2006,
        USB_PRODUCT_NINTENDO_SWITCH_JOYCON_PAIR = 0x2008,
        USB_PRODUCT_NINTENDO_SWITCH_JOYCON_RIGHT = 0x2007,
        USB_PRODUCT_NINTENDO_SWITCH_PRO = 0x2009,
        USB_PRODUCT_NINTENDO_WII_REMOTE = 0x0306,
        USB_PRODUCT_NINTENDO_WII_REMOTE2 = 0x0330,
        USB_PRODUCT_NVIDIA_SHIELD_CONTROLLER_V103 = 0x7210,
        USB_PRODUCT_NVIDIA_SHIELD_CONTROLLER_V104 = 0x7214,
        USB_PRODUCT_RAZER_ATROX = 0x0a00,
        USB_PRODUCT_RAZER_PANTHERA = 0x0401,
        USB_PRODUCT_RAZER_PANTHERA_EVO = 0x1008,
        USB_PRODUCT_RAZER_RAIJU = 0x1000,
        USB_PRODUCT_RAZER_TOURNAMENT_EDITION_USB = 0x1007,
        USB_PRODUCT_RAZER_TOURNAMENT_EDITION_BLUETOOTH = 0x100a,
        USB_PRODUCT_RAZER_ULTIMATE_EDITION_USB = 0x1004,
        USB_PRODUCT_RAZER_ULTIMATE_EDITION_BLUETOOTH = 0x1009,
        USB_PRODUCT_SHANWAN_DS3 = 0x0523,
        USB_PRODUCT_SONY_DS3 = 0x0268,
        USB_PRODUCT_SONY_DS4 = 0x05c4,
        USB_PRODUCT_SONY_DS4_DONGLE = 0x0ba0,
        USB_PRODUCT_SONY_DS4_SLIM = 0x09cc,
        USB_PRODUCT_SONY_DS5 = 0x0ce6,
        USB_PRODUCT_VICTRIX_FS_PRO_V2 = 0x0207,
        USB_PRODUCT_XBOX360_XUSB_CONTROLLER = 0x02a1,
        USB_PRODUCT_XBOX360_WIRED_CONTROLLER = 0x028e,
        USB_PRODUCT_XBOX360_WIRELESS_RECEIVER = 0x0719,
        USB_PRODUCT_XBOX_ONE_ADAPTIVE = 0x0b0a,
        USB_PRODUCT_XBOX_ONE_ADAPTIVE_BLUETOOTH = 0x0b0c,
        USB_PRODUCT_XBOX_ONE_ADAPTIVE_BLE = 0x0b21,
        USB_PRODUCT_XBOX_ONE_ELITE_SERIES_1 = 0x02e3,
        USB_PRODUCT_XBOX_ONE_ELITE_SERIES_2 = 0x0b00,
        USB_PRODUCT_XBOX_ONE_ELITE_SERIES_2_BLUETOOTH = 0x0b05,
        USB_PRODUCT_XBOX_ONE_ELITE_SERIES_2_BLE = 0x0b22,
        USB_PRODUCT_XBOX_ONE_S = 0x02ea,
        USB_PRODUCT_XBOX_ONE_S_REV1_BLUETOOTH = 0x02e0,
        USB_PRODUCT_XBOX_ONE_S_REV2_BLUETOOTH = 0x02fd,
        USB_PRODUCT_XBOX_ONE_S_REV2_BLE = 0x0b20,
        USB_PRODUCT_XBOX_SERIES_X = 0x0b12,
        USB_PRODUCT_XBOX_SERIES_X_BLE = 0x0b13,
        USB_PRODUCT_XBOX_SERIES_X_VICTRIX_GAMBIT = 0x02d6,
        USB_PRODUCT_XBOX_SERIES_X_PDP_BLUE = 0x02d9,
        USB_PRODUCT_XBOX_SERIES_X_PDP_AFTERGLOW = 0x02da,
        USB_PRODUCT_XBOX_SERIES_X_POWERA_FUSION_PRO2 = 0x4001,
        USB_PRODUCT_XBOX_SERIES_X_POWERA_SPECTRA = 0x4002,
        USB_PRODUCT_XBOX_ONE_XBOXGIP_CONTROLLER = 0x02ff,
        USB_PRODUCT_XBOX_ONE_XINPUT_CONTROLLER = 0x02fe,
        USB_PRODUCT_STEAM_VIRTUAL_GAMEPAD = 0x11ff
    }

}

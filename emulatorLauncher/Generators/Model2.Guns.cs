using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class Model2Generator : Generator
    {
        private bool _demulshooter = false;

        private void ConfigureModel2Guns(IniFile ini, byte[] bytes, string parentRom)
        {
            var guns = RawLightgun.GetRawLightguns();
            int realGunsCount = RawLightgun.GetUsableLightGunCount();
            bool trueLightgun = realGunsCount > 0;
            RawLightgun gun1 = null;
            RawLightgun gun2 = null;

            if (guns.Length < 1)
            {
                SimpleLogger.Instance.Warning("[GUNS] No gun or mouse found.");
                return;
            }

            SimpleLogger.Instance.Info("[GUNS] Configuring guns for model2.");

            if (guns.Any(g => g.Type == RawLighGunType.SindenLightgun))
                Guns.StartSindenSoftware();

            if (guns.Length == 1)
            {
                SimpleLogger.Instance.Info("[GUNS] Found 1 gun to configure.");
                gun1 = guns[0];
            }
            else if (guns.Length > 1)
            {
                gun1 = guns[0];
                gun2 = guns[1];
                SimpleLogger.Instance.Info("[GUNS] Found 2 guns to configure.");
            }

            if (SystemConfig.isOptSet("m2_rawinput_p1") && !string.IsNullOrEmpty(SystemConfig["m2_rawinput_p1"]))
            {
                int index_1 = SystemConfig["m2_rawinput_p1"].ToInteger();
                ini.WriteValue("Input", "RawDevP1", index_1.ToString());
                if (guns[index_1] != null)
                    gun1 = guns[index_1];
            }
            else
                ini.WriteValue("Input", "RawDevP1", gun1.Index.ToString());

            if (gun1.DevicePath != null)
                SimpleLogger.Instance.Info("[GUNS] Gun 1 assigned to: " + gun1.DevicePath);

            if (gun2 != null)
            {
                if (SystemConfig.isOptSet("m2_rawinput_p2") && !string.IsNullOrEmpty(SystemConfig["m2_rawinput_p2"]))
                {
                    int index_2 = SystemConfig["m2_rawinput_p2"].ToInteger();
                    ini.WriteValue("Input", "RawDevP2", index_2.ToString());
                    if (guns[index_2] != null)
                        gun1 = guns[index_2];
                }
                else
                    ini.WriteValue("Input", "RawDevP2", gun2.Index.ToString());

                if (gun2.DevicePath != null)
                    SimpleLogger.Instance.Info("[GUNS] Gun 2 assigned to: " + gun2.DevicePath);
            }

            // Demulshooter
            if (guns.Length > 1)
                _demulshooter = true;
            if (SystemConfig.isOptSet("use_demulshooter") && !SystemConfig.getOptBoolean("use_demulshooter"))
                _demulshooter = false;
            if (SystemConfig.getOptBoolean("use_demulshooter"))
                _demulshooter = true;

            if (_demulshooter && !SystemConfig.getOptBoolean("m2_rawinput"))
                ini.WriteValue("Input", "UseRawInput", "0");
            else
                BindBoolIniFeature(ini, "Input", "UseRawInput", "m2_rawinput", "1", "0");

            if (_demulshooter)
            {
                SimpleLogger.Instance.Info("[GUNS] Configuring DemulShooter for Model2.");
                Demulshooter.StartDemulshooter("m2emulator", "model2", _rom, gun1, gun2);
            }

            // Crosshairs
            string crosshairCfgPath = Path.Combine(_path, "artwork", "crosshairs");
            if (!Directory.Exists(crosshairCfgPath)) { try { Directory.CreateDirectory(crosshairCfgPath); } catch { } }
            string crosshairCfgFile = Path.Combine(_path, "artwork", "crosshairs", Path.GetFileNameWithoutExtension(_rom) + ".cfg");

            if (_demulshooter && SystemConfig.isOptSet("m2_crosshair") && !string.IsNullOrEmpty(SystemConfig["m2_crosshair"]) && CrossGames.Contains(Path.GetFileNameWithoutExtension(_rom)))
            {
                SimpleLogger.Instance.Info("[GUNS] Configuring DemulShooter Crosshairs for Model2.");

                ini.WriteValue("Renderer", "DrawCross", "0");

                using (StreamWriter writer = new StreamWriter(crosshairCfgFile))
                {
                    if (SystemConfig["m2_crosshair"] == "0")
                    {
                        writer.WriteLine("1");
                        writer.WriteLine("1");
                        writer.WriteLine("0");
                    }
                    else
                    {
                        writer.WriteLine(SystemConfig["m2_crosshair"]);
                        writer.WriteLine(SystemConfig["m2_crosshair"]);
                        writer.WriteLine("1");
                    }
                }
            }
            else
            {
                using (StreamWriter writer = new StreamWriter(crosshairCfgFile, true))
                {
                    writer.WriteLine("1");
                    writer.WriteLine("1");
                    writer.WriteLine("0");
                }
            }

            if (parentRom != null)
                SimpleLogger.Instance.Info("[GUNS] Configuring Model2 input file: " + parentRom + ".input");

            // Player index bytes
            bytes[1] = bytes[5] = bytes[9] = bytes[13] = bytes[17] = bytes[21] = bytes[25] = bytes[29] = bytes[33] = bytes[37] = 0x00;
            bytes[41] = bytes[45] = bytes[49] = bytes[53] = bytes[57] = bytes[61] = bytes[65] = bytes[69] = bytes[73] = bytes[77] = bytes[81] = bytes[85] = 0x00;

            bytes[0] = (byte)0xC8;      // up
            bytes[4] = (byte)0xD0;      // down
            bytes[8] = (byte)0xCB;      // left
            bytes[12] = (byte)0xCD;     // right
            bytes[16] = (byte)0x11;     // Z
            bytes[20] = (byte)0x2D;     // X
            bytes[24] = (byte)0x1C;     // Return
            bytes[28] = (byte)0x2F;     // V

            if (gun1.Type == RawLighGunType.MayFlashWiimote)
            {
                bytes[32] = (byte)0xC8;     // up
                bytes[36] = (byte)0xD0;     // down
            }
            else
            {
                bytes[32] = (byte)0x02;     // 1
                bytes[36] = (byte)0x06;     // 5
            }

            bytes[40] = (byte)0xC8;     // up
            bytes[44] = (byte)0xD0;     // down
            bytes[48] = (byte)0xCB;     // left
            bytes[52] = (byte)0xCD;     // right
            bytes[56] = (byte)0x2A;     // maj
            bytes[60] = (byte)0x1D;     // CTRL
            bytes[64] = (byte)0x38;     // ALT
            bytes[68] = (byte)0x39;     // SPACE

            if (gun2.Type == RawLighGunType.MayFlashWiimote)
            {
                bytes[72] = (byte)0xCB;     // left
                bytes[76] = (byte)0xCD;     // right
            }
            else
            {
                bytes[72] = (byte)0x03;     // 2
                bytes[76] = (byte)0x07;     // 6
            }

            bytes[80] = (byte)0x3B;     // F1
            bytes[84] = (byte)0x3C;     // F2
            bytes[88] = (byte)0x42;     // F8
            bytes[92] = (byte)0x41;     // F7
            bytes[96] = (byte)0x40;     // F6
        }

        private static readonly List<string> CrossGames = new List<string>()
        { "bel", "hotd", "vcop", "vcop2" };
    }
}

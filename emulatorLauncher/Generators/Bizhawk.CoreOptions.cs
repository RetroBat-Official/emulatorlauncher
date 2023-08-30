using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Windows.Forms;
using System.Drawing;
using System.Management;
using emulatorLauncher.Tools;
using SharpDX.DirectInput;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private void SetupCoreOptions(DynamicJson json, string system, string core, string rom)
        {
            var coreSettings = json.GetOrCreateContainer("CoreSettings");
            var coreSyncSettings = json.GetOrCreateContainer("CoreSyncSettings");

            // NES
            ConfigureQuickNES(json, coreSettings, coreSyncSettings, core, system);
            ConfigureNesHawk(json, coreSettings, coreSyncSettings, core, system);

            // SNES
            ConfigureFaust(json, coreSettings, coreSyncSettings, core, system);
            ConfigureSnes9x(json, coreSettings, coreSyncSettings, core, system);
            ConfigureBsnes(json, coreSettings, coreSyncSettings, core, system);

            // MASTER SYSTEM
            ConfigureSmsHawk(json, coreSettings, coreSyncSettings, core, system);

            // MEGADRIVE
            ConfigureGenesisPlusGX(json, coreSettings, coreSyncSettings, core, system);

            // SATURN
            ConfigureSaturnus(json, coreSettings, coreSyncSettings, core, system);

            // PC ENGINE
            ConfigureTurboNyma(json, coreSettings, coreSyncSettings, core, system);
            ConfigureHyperNyma(json, coreSettings, coreSyncSettings, core, system);
            ConfigurePCEHawk(json, coreSettings, coreSyncSettings, core, system);

            // GAME BOY / COLOR
            ConfigureSameboy(json, coreSettings, coreSyncSettings, core, system);
            ConfigureGambatte(json, coreSettings, coreSyncSettings, core, system);
            ConfigureGBHawk(json, coreSettings, coreSyncSettings, core, system);

            // GBA
            ConfiguremGBA(json, coreSettings, coreSyncSettings, core, system);

            // N64
            ConfigureMupen64(json, coreSettings, coreSyncSettings, core, system);
            ConfigureAres64(json, coreSettings, coreSyncSettings, core, system);

            // NDS
            ConfigureMelonDS(json, coreSettings, coreSyncSettings, core, system);

            // NEO GEO POCKET
            ConfigureNeopop(json, coreSettings, coreSyncSettings, core, system);
        }

        private void ConfigureQuickNES(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "QuickNes")
                return;

            var quickNesSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES");
            quickNesSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES+QuickNESSyncSettings, BizHawk.Emulation.Cores";
            quickNesSync["LeftPortConnected"] = "true";
            quickNesSync["RightPortConnected"] = "true";
        }

        private void ConfigureNesHawk(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "NesHawk")
                return;

            var nesHawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.NES.NES");
            nesHawkSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.NES.NES+NESSyncSettings, BizHawk.Emulation.Cores";

            if (SystemConfig.isOptSet("nes_region") && !string.IsNullOrEmpty(SystemConfig["nes_region"]))
                nesHawkSync["RegionOverride"] = SystemConfig["nes_region"];
            else
                nesHawkSync["RegionOverride"] = "0";

            var nesHawkControls = nesHawkSync.GetOrCreateContainer("Controls");
            nesHawkControls["NesLeftPort"] = "ControllerNES";
            nesHawkControls["NesRightPort"] = "ControllerNES";

            if (Controllers.Count > 2)
            {
                nesHawkControls["Famicom"] = "true";
                nesHawkControls["FamicomExpPort"] = "Famicom4P";
            }
            else
            {
                nesHawkControls["Famicom"] = "false";
                nesHawkControls["FamicomExpPort"] = "UnpluggedFam";
            }

            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
            {
                string devicetype = "Zapper";
                nesHawkControls["Famicom"] = "false";
                nesHawkControls["NesLeftPort"] = "ControllerNES";
                nesHawkControls["NesRightPort"] = "Zapper";
                nesHawkControls["FamicomExpPort"] = "UnpluggedFam";
                SetupLightGuns(json, devicetype, core, system, 2);
            }
        }

        private void ConfigureFaust(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Faust")
                return;

            var faustSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.Faust.Faust");
            var mednafenValues = faustSync.GetOrCreateContainer("MednafenValues");
            if (Controllers.Count > 5)
            {
                mednafenValues["snes_faust.input.sport1.multitap"] = "1";
                mednafenValues["snes_faust.input.sport2.multitap"] = "1";
            }
            else if (Controllers.Count > 2)
            {
                mednafenValues["snes_faust.input.sport1.multitap"] = "0";
                mednafenValues["snes_faust.input.sport2.multitap"] = "1";
            }
            else
            {
                mednafenValues["snes_faust.input.sport1.multitap"] = "0";
                mednafenValues["snes_faust.input.sport2.multitap"] = "0";
            }
        }

        private void ConfigureSnes9x(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Snes9x")
                return;

            var snes9xSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.SNES9X.Snes9x");
            snes9xSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.SNES9X.Snes9x+SyncSettings, BizHawk.Emulation.Cores";

            snes9xSync["LeftPort"] = "1";

            if (Controllers.Count > 2)
                snes9xSync["RightPort"] = "2";
            else
                snes9xSync["RightPort"] = "1";

            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
            {
                string devicetype = "4";
                snes9xSync["LeftPort"] = "1";
                snes9xSync["RightPort"] = "4";
                SetupLightGuns(json, devicetype, core, system, 2);
            }
        }

        private void ConfigureBsnes(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "BSNES")
                return;

            var bsnesSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.SNES.LibsnesCore");

            bsnesSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.SNES.LibsnesCore+SnesSyncSettings, BizHawk.Emulation.Cores";
            if (Controllers.Count > 5)
            {
                bsnesSync["LeftPort"] = "2";
                bsnesSync["RightPort"] = "2";
            }
            else if (Controllers.Count > 2)
            {
                bsnesSync["LeftPort"] = "1";
                bsnesSync["RightPort"] = "2";
            }
            else
            {
                bsnesSync["LeftPort"] = "1";
                bsnesSync["RightPort"] = "1";
            }

            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
            {
                string devicetype = "4";
                bsnesSync["LeftPort"] = "1";
                bsnesSync["RightPort"] = "4";
                SetupLightGuns(json, devicetype, core, system, 2);
            }
        }

        private void ConfigureSmsHawk(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "SMSHawk")
                return;

            var smsHawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Sega.MasterSystem.SMS");
            smsHawkSync["$type"] = "BizHawk.Emulation.Cores.Sega.MasterSystem.SMS+SmsSyncSettings, BizHawk.Emulation.Cores";
            smsHawkSync["EnableFm"] = "true";
            smsHawkSync["Port1"] = "0";
            smsHawkSync["Port2"] = "0";

            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
            {
                string devicetype = "3";
                smsHawkSync["Port1"] = "3";
                smsHawkSync["Port2"] = "0";
                SetupLightGuns(json, devicetype, core, system, 1);
            }
        }

        private void ConfigureGenesisPlusGX(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Genplus-gx")
                return;

            var genplusgxSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Sega.gpgx.GPGX");
            genplusgxSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Sega.gpgx.GPGX+GPGXSyncSettings, BizHawk.Emulation.Cores";
            genplusgxSync["UseSixButton"] = "true";

            if (Controllers.Count > 5)
            {
                genplusgxSync["ControlTypeLeft"] = "4";
                genplusgxSync["ControlTypeRight"] = "4";
            }
            else if (Controllers.Count > 2)
            {
                genplusgxSync["ControlTypeLeft"] = "1";
                genplusgxSync["ControlTypeRight"] = "4";
            }
            else
            {
                genplusgxSync["ControlTypeLeft"] = "1";
                genplusgxSync["ControlTypeRight"] = "1";
            }
        }

        private void ConfigureSaturnus(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Saturnus")
                return;

            var saturnSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Sega.Saturn.Saturnus");
            var mednafenValues = saturnSync.GetOrCreateContainer("MednafenValues");
            saturnSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            if (Controllers.Count > 7)
            {
                mednafenValues["ss.input.sport1.multitap"] = "1";
                mednafenValues["ss.input.sport2.multitap"] = "1";
            }
            else if (Controllers.Count > 2)
            {
                mednafenValues["ss.input.sport1.multitap"] = "0";
                mednafenValues["ss.input.sport2.multitap"] = "1";
            }
            else
            {
                mednafenValues["ss.input.sport1.multitap"] = "0";
                mednafenValues["ss.input.sport2.multitap"] = "0";
            }
        }

        private void ConfigureTurboNyma(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "TurboNyma")
                return;

            var turboNymaSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.NEC.PCE.TurboNyma");
            turboNymaSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            var mednafenValues = turboNymaSync.GetOrCreateContainer("MednafenValues");
            if (Controllers.Count > 1)
                mednafenValues["pce.input.multitap"] = "1";
            else
                mednafenValues["pce.input.multitap"] = "0";

            var portDevices = turboNymaSync.GetOrCreateContainer("PortDevices");
            if (Controllers.Count > 0)
            {
                for (int i = 0; i < Controllers.Count; i++)
                    portDevices[i.ToString()] = "gamepad";
            }
        }

        private void ConfigureHyperNyma(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "HyperNyma")
                return;

            var hyperNymaSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.NEC.PCE.HyperNyma");
            var portDevices = hyperNymaSync.GetOrCreateContainer("PortDevices");
            hyperNymaSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            if (Controllers.Count > 0)
            {
                for (int i = 0; i < Controllers.Count ; i++)
                    portDevices[i.ToString()] = "gamepad";
            }
        }

        private void ConfigurePCEHawk(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "PCEHawk")
                return;

            var pceHawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.PCEngine.PCEngine");
            pceHawkSync["$type"] = "BizHawk.Emulation.Cores.PCEngine.PCEngine+PCESyncSettings, BizHawk.Emulation.Cores";

            if (Controllers.Count > 0)
            {
                for (int i = 1; i < Controllers.Count + 1; i++)
                    pceHawkSync["Port" + i] = "1";
            }
        }

        private void ConfigureSameboy(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "SameBoy")
                return;

            var sameboySync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.Sameboy.Sameboy");
            sameboySync["$type"] = "BizHawk.Emulation.Cores.Nintendo.Sameboy.Sameboy+SameboySyncSettings, BizHawk.Emulation.Cores";
            sameboySync["ConsoleMode"] = "-1";
        }

        private void ConfigureGambatte(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Gambatte")
                return;

            var gambatteSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.Gameboy.Gameboy");
            gambatteSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.Gameboy.Gameboy+GambatteSyncSettings, BizHawk.Emulation.Cores";
            gambatteSync["ConsoleMode"] = "0";
        }

        private void ConfigureGBHawk(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "GBHawk")
                return;

            var gbhawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.GBHawk.GBHawk");
            gbhawkSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.GBHawk.GBHawk+GBSyncSettings, BizHawk.Emulation.Cores";
            gbhawkSync["ConsoleMode"] = "0";
        }

        private void ConfiguremGBA(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "mGBA")
                return;

            var gbahawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.GBA.MGBAHawk");
            gbahawkSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.GBA.MGBAHawk+SyncSettings, BizHawk.Emulation.Cores";
            gbahawkSync["SkipBios"] = "true";
        }

        private void ConfigureMupen64(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Mupen64Plus")
                return;

            var mupen64Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.N64.N64");
            var n64controller = mupen64Sync.GetOrCreateContainer("Controllers");
            var ricePlugin = mupen64Sync.GetOrCreateContainer("RicePlugin");
            var glidePlugin = mupen64Sync.GetOrCreateContainer("GlidePlugin");
            var glidemk2Plugin = mupen64Sync.GetOrCreateContainer("Glide64mk2Plugin");
            var glide64Plugin = mupen64Sync.GetOrCreateContainer("GLideN64Plugin");
            var angrylionPlugin = mupen64Sync.GetOrCreateContainer("AngrylionPlugin");

            mupen64Sync["$type"] = "BizHawk.Emulation.Cores.Nintendo.N64.N64SyncSettings, BizHawk.Emulation.Cores";

            
        }

        private void ConfigureAres64(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Ares64")
                return;

            var ares64Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.Ares64.Ares64");
            ares64Sync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.Ares64.Ares64+Ares64SyncSettings, BizHawk.Emulation.Cores";
        }

        private void ConfigureMelonDS(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "melonDS")
                return;

            var melonDSSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS");
            melonDSSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS+NDSSyncSettings, BizHawk.Emulation.Cores";
        }

        private void ConfigureNeopop(DynamicJson json, DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "NeoPop")
                return;

            var neopopSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.SNK.NeoGeoPort");
            neopopSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";
        }
    }
}
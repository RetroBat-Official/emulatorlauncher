using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Linq;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private void SetupCoreOptions(DynamicJson json, string system, string core, string rom)
        {
            var coreSettings = json.GetOrCreateContainer("CoreSettings");
            var coreSyncSettings = json.GetOrCreateContainer("CoreSyncSettings");

            ConfigureQuickNES(json, coreSettings, coreSyncSettings, core, system);
            ConfigureNesHawk(json, coreSettings, coreSyncSettings, core, system);
            ConfigureFaust(json, coreSettings, coreSyncSettings, core, system);
            ConfigureSnes9x(json, coreSettings, coreSyncSettings, core, system);
            ConfigureBsnes(json, coreSettings, coreSyncSettings, core, system);
            ConfigureGenesisPlusGX(json, coreSettings, coreSyncSettings, core, system);
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
    }
}
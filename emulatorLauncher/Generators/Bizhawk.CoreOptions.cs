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
            var coreSyncSettings = json.GetOrCreateContainer("CoreSyncSettings");
            ConfigureQuickNES(json, coreSyncSettings, core, system);
            ConfigureNesHawk(json, coreSyncSettings, core, system);
        }

        private void ConfigureQuickNES(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "QuickNes")
                return;

            var quickNesSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES");
            quickNesSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.QuickNES.QuickNES+QuickNESSyncSettings, BizHawk.Emulation.Cores";
            quickNesSync["LeftPortConnected"] = "true";
            quickNesSync["RightPortConnected"] = "true";
        }

        private void ConfigureNesHawk(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
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
        }
    }
}
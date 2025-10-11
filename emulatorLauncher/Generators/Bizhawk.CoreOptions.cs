﻿using System.Collections.Generic;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    partial class BizhawkGenerator : Generator
    {
        private void SetupCoreOptions(DynamicJson json, string system, string core, string rom)
        {
            var coreSettings = json.GetOrCreateContainer("CoreSettings");
            var coreSyncSettings = json.GetOrCreateContainer("CoreSyncSettings");

            // 3DS
            ConfigureEncore(coreSettings, coreSyncSettings, core);

            // ATARI
            ConfigureAtari2600(coreSyncSettings, core);
            ConfigureAtari7800(coreSyncSettings, core);
            ConfigureJaguar(coreSyncSettings, core);

            // INTV
            Configureintv(coreSyncSettings, core);

            // channelF
            ConfigurechannelF(coreSyncSettings, core);

            // NES
            ConfigureQuickNES(coreSyncSettings, core);
            ConfigureNesHawk(json, coreSyncSettings, core, system);

            // SNES
            ConfigureFaust(coreSyncSettings, core);
            ConfigureSnes9x(json, coreSyncSettings, core, system);
            ConfigureBsnes(json, coreSyncSettings, core, system);

            // MASTER SYSTEM + GAMEGEAR
            ConfigureSmsHawk(json, coreSyncSettings, core, system);

            // MEGADRIVE + 32X
            ConfigureGenesisPlusGX(coreSyncSettings, core);
            ConfigurePicoDrive(coreSyncSettings, core);

            // SATURN
            ConfigureSaturnus(json, coreSyncSettings, core, system);

            // PC ENGINE
            ConfigureTurboNyma(coreSyncSettings, core);
            ConfigureHyperNyma(coreSyncSettings, core, system);
            ConfigurePCEHawk(coreSyncSettings, core);

            // GAME BOY / COLOR
            ConfigureSameboy(coreSyncSettings, core, system);
            ConfigureGambatte(coreSyncSettings, core, system);
            ConfigureGBHawk(coreSyncSettings, core);

            // GBA
            ConfiguremGBA(coreSyncSettings, core);

            // N64
            ConfigureMupen64(coreSettings, coreSyncSettings, core);
            ConfigureAres64(coreSyncSettings, core);

            // NDS
            ConfigureMelonDS(coreSettings, coreSyncSettings, core, rom);

            // NEO GEO POCKET
            ConfigureNeopop(coreSyncSettings, core);

            // COLECOVISION
            ConfigureColecovision(coreSyncSettings, core);

            // PCFX
            ConfigurePcfx(coreSyncSettings, core);

            // PSX
            ConfigureNymashock(coreSyncSettings, core);
            ConfigureOctoshock(coreSettings, coreSyncSettings, core);

            // Odyssey 2
            ConfigureO2Hawk(coreSyncSettings, core);

            // TIC-80
            Configuretic80(coreSyncSettings, core);

            // Virtual Boy
            ConfigureticVirtualBoyee(coreSyncSettings, core);

            // WSWAN - WSWANC
            ConfigureCygne(coreSyncSettings, core);

            // ZX Spectrum
            ConfigureZXHawk(coreSyncSettings, core);
        }

        private void ConfigureEncore(DynamicJson coreSettings, DynamicJson coreSyncSettings, string core)
        {
            if (core != "Encore")
                return;

            var encoreCoreSettings = coreSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.N3DS.Encore");
            var encoreSyncSettings = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.N3DS.Encore");

            // Core settings
            encoreCoreSettings["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.N3DS.Encore+EncoreSettings, BizHawk.Emulation.Cores";
            BindFeature(encoreCoreSettings, "TextureFilter", "bizhawk_3ds_texturefilter", "0");
            BindFeature(encoreCoreSettings, "TextureSampling", "bizhawk_3ds_texturesampling", "0");
            BindBoolFeatureOn(encoreCoreSettings, "FilterMode", "bizhawk_3ds_filtermode", "true", "false");
            BindFeature(encoreCoreSettings, "LayoutOption", "bizhawk_3ds_layoutmode", "0");
            BindBoolFeature(encoreCoreSettings, "SwapScreen", "bizhawk_3ds_swapscreen", "true", "false");
            BindBoolFeature(encoreCoreSettings, "UprightScreen", "bizhawk_3ds_vertical", "true", "false");

            // Sync settings
            encoreSyncSettings["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.N3DS.Encore+EncoreSyncSettings, BizHawk.Emulation.Cores";
            BindBoolFeature(encoreSyncSettings, "UseCpuJit", "bizhawk_3ds_cpuJIT", "false", "true");
            BindBoolFeature(encoreSyncSettings, "GraphicsApi", "bizhawk_3ds_renderer", "0", "1");
            BindBoolFeatureOn(encoreSyncSettings, "AsyncShaderCompilation", "bizhawk_3ds_asyncshaders", "true", "false");
            BindBoolFeature(encoreSyncSettings, "UseVirtualSd", "bizhawk_3ds_virtualSD", "false", "true");
            BindBoolFeatureOn(encoreSyncSettings, "IsNew3ds", "bizhawk_3ds_new3ds", "true", "false");
            BindFeature(encoreSyncSettings, "RegionValue", "bizhawk_3ds_region", "-1");
            BindFeature(encoreSyncSettings, "CFGSystemLanguage", "bizhawk_3ds_language", "1");
            encoreSyncSettings["CFGUsername"] = "RETROBAT";
        }

        private void ConfigureAtari2600(DynamicJson coreSyncSettings, string core)
        {
            if (core != "A26" && core != "Atari2600Hawk")
                return;

            var a2600Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Atari.Atari2600.Atari2600");
            a2600Sync["$type"] = "BizHawk.Emulation.Cores.Atari.Atari2600.Atari2600+A2600SyncSettings, BizHawk.Emulation.Cores";
            a2600Sync["Port1"] = "1";
            a2600Sync["Port2"] = "1";

            BindFeature(a2600Sync, "LeftDifficulty", "bizhawk_A26_difficulty", "true");
            BindFeature(a2600Sync, "RightDifficulty", "bizhawk_A26_difficulty", "true");
        }

        private void ConfigureAtari7800(DynamicJson coreSyncSettings, string core)
        {
            if (core != "A78")
                return;

            var a7800Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Atari.A7800Hawk.A7800Hawk");
            a7800Sync["$type"] = "BizHawk.Emulation.Cores.Atari.A7800Hawk.A7800Hawk+A7800SyncSettings, BizHawk.Emulation.Cores";
            a7800Sync["_port1"] = "Joystick Controller";
            a7800Sync["_port2"] = "Joystick Controller";
        }

        private void ConfigureJaguar(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Jaguar")
                return;

            var jaguarSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Atari.Jaguar.VirtualJaguar");
            jaguarSync["$type"] = "BizHawk.Emulation.Cores.Atari.Jaguar.VirtualJaguar+VirtualJaguarSyncSettings, BizHawk.Emulation.Cores";
            jaguarSync["P1Active"] = "true";
            jaguarSync["P2Active"] = "true";

            BindBoolFeature(jaguarSync, "NTSC", "bizhawk_jaguar_forcePAL", "false", "true");
        }

        private void Configureintv(DynamicJson coreSyncSettings, string core)
        {
            if (core != "IntelliHawk")
                return;

            var intvSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Intellivision.Intellivision");
            intvSync["$type"] = "BizHawk.Emulation.Cores.Intellivision.Intellivision+IntvSyncSettings, BizHawk.Emulation.Cores";

            if (Program.SystemConfig.isOptSet("bizhawk_intv_padtype") && !string.IsNullOrEmpty(Program.SystemConfig["bizhawk_intv_padtype"]))
            {
                intvSync["_port1"] = Program.SystemConfig["bizhawk_intv_padtype"];
                intvSync["_port2"] = Program.SystemConfig["bizhawk_intv_padtype"];
            }
            else
            {
                intvSync["_port1"] = "Standard (Analog Disc)";
                intvSync["_port2"] = "Standard (Analog Disc)";
            }
        }

        private void ConfigurechannelF(DynamicJson coreSyncSettings, string core)
        {
            if (core != "ChannelFHawk")
                return;

            var channelfSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.ChannelF.ChannelF");
            channelfSync["$type"] = "BizHawk.Emulation.Cores.Consoles.ChannelF.ChannelF+ChannelFSyncSettings, BizHawk.Emulation.Cores";

            if (Program.SystemConfig.isOptSet("bizhawk_channelf_region") && !string.IsNullOrEmpty(Program.SystemConfig["bizhawk_channelf_region"]))
                channelfSync["Region"] = Program.SystemConfig["bizhawk_channelf_region"];
            else
                channelfSync["Region"] = "0";

            if (Program.SystemConfig.isOptSet("bizhawk_channelf_version") && !string.IsNullOrEmpty(Program.SystemConfig["bizhawk_channelf_version"]))
                channelfSync["Version"] = Program.SystemConfig["bizhawk_channelf_version"];
            else
                channelfSync["Version"] = "0";
        }

        private void ConfigureQuickNES(DynamicJson coreSyncSettings, string core)
        {
            if (core != "QuickNes" && core != "quickerNES")
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

        private void ConfigureFaust(DynamicJson coreSyncSettings, string core)
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

        private void ConfigureSnes9x(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
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

        private void ConfigureBsnes(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
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

        private void ConfigureSmsHawk(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "SMSHawk")
                return;

            var smsHawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Sega.MasterSystem.SMS");
            smsHawkSync["$type"] = "BizHawk.Emulation.Cores.Sega.MasterSystem.SMS+SmsSyncSettings, BizHawk.Emulation.Cores";


            BindBoolFeature(smsHawkSync, "EnableFm", "bizhawk_sms_fm", "true", "false");
            BindBoolFeature(smsHawkSync, "UseBios", "bizhawk_sms_bios", "true", "false");
            BindFeature(smsHawkSync, "ConsoleRegion", "bizhawk_sms_region", "3");
            BindFeature(smsHawkSync, "DisplayType", "bizhawk_sms_format", "2");

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

        private void ConfigureGenesisPlusGX(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Genplus-gx")
                return;

            var genplusgxSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Sega.gpgx.GPGX");
            genplusgxSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Sega.gpgx.GPGX+GPGXSyncSettings, BizHawk.Emulation.Cores";
            
            BindBoolFeature(genplusgxSync, "UseSixButton", "md_3buttons", "false", "true");
            BindFeature(genplusgxSync, "Region", "bizhawk_md_region", "0");
            BindBoolFeature(genplusgxSync, "Filter", "bizhawk_md_lowpass", "1", "0");

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

        private void ConfigurePicoDrive(DynamicJson coreSyncSettings, string core)
        {
            if (core != "PicoDrive")
                return;

            var picoSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Sega.PicoDrive.PicoDrive");
            picoSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Sega.PicoDrive.PicoDrive+SyncSettings, BizHawk.Emulation.Cores";

            BindFeature(picoSync, "RegionOverride", "bizhawk_pico_region", "0");
        }

        private void ConfigureSaturnus(DynamicJson json, DynamicJson coreSyncSettings, string core, string system)
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

            if (SystemConfig.isOptSet("bizhawk_saturn_region") && !string.IsNullOrEmpty(SystemConfig["bizhawk_saturn_region"]))
            {
                mednafenValues["ss.region_autodetect"] = "0";
                mednafenValues["ss.region_default"] = SystemConfig["bizhawk_saturn_region"];
            }
            else
            {
                mednafenValues["ss.region_autodetect"] = "1";
                mednafenValues["ss.region_default"] = "jp";
            }

            BindFeature(mednafenValues, "ss.cart", "bizhawk_saturn_expansion", "auto");
            BindFeature(mednafenValues, "ss.smpc.autortc.lang", "bizhawk_saturn_language", "english");

            var portDevices = saturnSync.GetOrCreateContainer("PortDevices");

            for (int i = 0; i < 12; i++)
            {
                portDevices.Remove(i.ToString());
            }

            if (SystemConfig.isOptSet("use_guns") && SystemConfig.getOptBoolean("use_guns"))
            {
                string devicetype = "gun";
                portDevices["0"] = "gun";

                SetupLightGuns(json, devicetype, core, system);
            }
        }

        private void ConfigureTurboNyma(DynamicJson coreSyncSettings, string core)
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

        private void ConfigureHyperNyma(DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "HyperNyma")
                return;

            var hyperNymaSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.NEC.PCE.HyperNyma");
            var portDevices = hyperNymaSync.GetOrCreateContainer("PortDevices");
            hyperNymaSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            var mednafenValues = hyperNymaSync.GetOrCreateContainer("MednafenValues");

            if (Controllers.Count > 0)
            {
                for (int i = 0; i < Controllers.Count ; i++)
                    portDevices[i.ToString()] = "gamepad";
            }

            if (system == "supergrafx")
                mednafenValues["pce_fast.forcesgx"] = "1";
            else
                mednafenValues["pce_fast.forcesgx"] = "0";
        }

        private void ConfigurePCEHawk(DynamicJson coreSyncSettings, string core)
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

        private void ConfigureSameboy(DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "SameBoy")
                return;

            var sameboySync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.Sameboy.Sameboy");
            sameboySync["$type"] = "BizHawk.Emulation.Cores.Nintendo.Sameboy.Sameboy+SameboySyncSettings, BizHawk.Emulation.Cores";
            sameboySync["ConsoleMode"] = "-1";

            string gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gb_bios.bin");
            if (system == "gbc")
                gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gbc_bios.bin");
            else if (system == "gba")
                gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gba_bios.bin");

            if (File.Exists(gbBios) && SystemConfig.getOptBoolean("bizhawk_gb_bios"))
                sameboySync["EnableBIOS"] = "true";
            else
                sameboySync["EnableBIOS"] = "false";
        }

        private void ConfigureGambatte(DynamicJson coreSyncSettings, string core, string system)
        {
            if (core != "Gambatte")
                return;

            var gambatteSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.Gameboy.Gameboy");
            gambatteSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.Gameboy.Gameboy+GambatteSyncSettings, BizHawk.Emulation.Cores";
            gambatteSync["ConsoleMode"] = "0";

            string gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gb_bios.bin");
            if (system == "gbc")
                gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gbc_bios.bin");
            else if (system == "gba")
                gbBios = Path.Combine(AppConfig.GetFullPath("bios"), "gba_bios.bin");

            if (File.Exists(gbBios) && SystemConfig.getOptBoolean("bizhawk_gb_bios"))
                gambatteSync["EnableBIOS"] = "true";
            else
                gambatteSync["EnableBIOS"] = "false";
        }

        private void ConfigureGBHawk(DynamicJson coreSyncSettings, string core)
        {
            if (core != "GBHawk")
                return;

            var gbhawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.GBHawk.GBHawk");
            gbhawkSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.GBHawk.GBHawk+GBSyncSettings, BizHawk.Emulation.Cores";
            gbhawkSync["ConsoleMode"] = "0";
        }

        private void ConfiguremGBA(DynamicJson coreSyncSettings, string core)
        {
            if (core != "mGBA")
                return;

            var gbahawkSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.GBA.MGBAHawk");
            gbahawkSync["$type"] = "BizHawk.Emulation.Cores.Nintendo.GBA.MGBAHawk+SyncSettings, BizHawk.Emulation.Cores";
            gbahawkSync["SkipBios"] = "true";

            BindBoolFeatureOn(gbahawkSync, "SkipBios", "bizhawk_gba_skipbios", "true", "false");
        }

        private void ConfigureMupen64(DynamicJson coreSettings, DynamicJson coreSyncSettings, string core)
        {
            if (core != "Mupen64Plus")
                return;

            var n64CoreSettings = coreSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.N64.N64");
            n64CoreSettings["$type"] = "BizHawk.Emulation.Cores.Nintendo.N64.N64Settings, BizHawk.Emulation.Cores";

            if (SystemConfig.isOptSet("bizhawk_n64_resolution") && !string.IsNullOrEmpty(SystemConfig["bizhawk_n64_resolution"]))
            {
                string res = SystemConfig["bizhawk_n64_resolution"];
                string[] parts = res.Split('x');
                
                if (parts.Length > 1)
                {
                    string width = parts[0];
                    string height = parts[1];

                    n64CoreSettings["VideoSizeX"] = width;
                    n64CoreSettings["VideoSizeY"] = height;
                }
            }
            else
            {
                n64CoreSettings["VideoSizeX"] = "320";
                n64CoreSettings["VideoSizeY"] = "240";
            }

            var mupen64Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Nintendo.N64.N64");

            mupen64Sync["$type"] = "BizHawk.Emulation.Cores.Nintendo.N64.N64SyncSettings, BizHawk.Emulation.Cores";

            BindFeature(mupen64Sync, "Core", "bizhawk_n64_cpucore", "1");
            BindFeature(mupen64Sync, "Rsp", "bizhawk_n64_rsp", "0");
            BindFeature(mupen64Sync, "VideoPlugin", "bizhawk_n64_gfx", "4");

            mupen64Sync.Remove("Controllers");
            var n64controller = new List<DynamicJson>();
            
            for (int i = 1; i <= 4; i++)
            {
                var pack = new DynamicJson();
                if (SystemConfig.isOptSet("bizhawk_n64_pak" + i) && !string.IsNullOrEmpty(SystemConfig["bizhawk_n64_pak" + i]))
                {
                    pack["PakType"] = SystemConfig["bizhawk_n64_pak" + i];
                    pack["IsConnected"] = "true";
                }

                else
                {
                    pack["PakType"] = "1";
                    pack["IsConnected"] = "true";
                }

                n64controller.Add(pack);
            }

            mupen64Sync.SetObject("Controllers", n64controller);

            /*
            var ricePlugin = mupen64Sync.GetOrCreateContainer("RicePlugin");
            var glidePlugin = mupen64Sync.GetOrCreateContainer("GlidePlugin");
            var glidemk2Plugin = mupen64Sync.GetOrCreateContainer("Glide64mk2Plugin");
            var glide64Plugin = mupen64Sync.GetOrCreateContainer("GLideN64Plugin");
            var angrylionPlugin = mupen64Sync.GetOrCreateContainer("AngrylionPlugin");
            */
        }

        private void ConfigureAres64(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Ares64")
                return;

            var ares64Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.Ares64.Ares64");
            ares64Sync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.Ares64.Ares64+Ares64SyncSettings, BizHawk.Emulation.Cores";

            BindFeature(ares64Sync, "P1Controller", "bizhawk_n64_pak1", "1");
            BindFeature(ares64Sync, "P2Controller", "bizhawk_n64_pak2", "1");
            BindFeature(ares64Sync, "P3Controller", "bizhawk_n64_pak3", "1");
            BindFeature(ares64Sync, "P4Controller", "bizhawk_n64_pak4", "1");

            BindFeature(ares64Sync, "CPUEmulation", "bizhawk_n64_cpucore", "1");
        }

        private void ConfigureMelonDS(DynamicJson coreSettings, DynamicJson coreSyncSettings, string core, string rom)
        {
            if (core != "melonDS")
                return;

            var melonDS = coreSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS");
            melonDS["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS+NDSSettings, BizHawk.Emulation.Cores";

            BindFeature(melonDS, "ScreenLayout", "bizhawk_melonds_layout", "0");
            BindBoolFeature(melonDS, "ScreenInvert", "bizhawk_melonds_screeninvert", "true", "false");
            BindFeature(melonDS, "ScreenRotation", "bizhawk_melonds_rotate", "0");

            var melonDSSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS");
            melonDSSync["$type"] = "BizHawk.Emulation.Cores.Consoles.Nintendo.NDS.NDS+NDSSyncSettings, BizHawk.Emulation.Cores";

            BindBoolFeature(melonDSSync, "UseRealBIOS", "bizhawk_melonds_externalBIOS", "true", "false");
            BindBoolFeature(melonDSSync, "SkipFirmware", "bizhawk_melonds_boottobios", "false", "true");
            melonDSSync["ClearNAND"] = "false";

            bool bootToDSINand = Path.GetExtension(rom).ToLowerInvariant() == ".bin";
            bool dsi = SystemConfig.isOptSet("bizhawk_melonds_dsi") && SystemConfig["bizhawk_melonds_dsi"] == "1";

            if (bootToDSINand)
                dsi = true;

            if (dsi)
            {
                melonDSSync["UseDSi"] = "true";
                if (bootToDSINand)
                {
                    melonDSSync["SkipFirmware"] = "false";
                    melonDSSync["UseRealBIOS"] = "false";

                    // Copy the loaded nand to the bios folder before loading, so that multiple nand files can be used.
                    string biosPath = Path.Combine(AppConfig.GetFullPath("bios"));
                    if (!string.IsNullOrEmpty(biosPath))
                    {
                        string nandFileTarget = Path.Combine(AppConfig.GetFullPath("bios"), "dsi_nand.bin");
                        string nandFileSource = rom;

                        if (File.Exists(nandFileTarget) && File.Exists(nandFileSource))
                            File.Delete(nandFileTarget);

                        if (File.Exists(nandFileSource))
                            File.Copy(nandFileSource, nandFileTarget);
                    }
                }
            }
            else
                melonDSSync["UseDSi"] = "false";
        }

        private void ConfigureNeopop(DynamicJson coreSyncSettings, string core)
        {
            if (core != "NeoPop")
                return;

            var neopopSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.SNK.NeoGeoPort");
            neopopSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            var mednafenValues = neopopSync.GetOrCreateContainer("MednafenValues");

            BindFeature(mednafenValues, "ngp.language", "bizhawk_ngp_language", "english");
        }

        private void ConfigureColecovision(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Coleco")
                return;

            var colecoSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.ColecoVision.ColecoVision");
            colecoSync["$type"] = "BizHawk.Emulation.Cores.ColecoVision.ColecoVision+ColecoSyncSettings, BizHawk.Emulation.Cores";
            colecoSync["_port1"] = "ColecoVision Basic Controller";
            colecoSync["_port2"] = "ColecoVision Basic Controller";

        }

        private void ConfigurePcfx(DynamicJson coreSyncSettings, string core)
        {
            if (core != "PCFX")
                return;

            var pcfxSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.NEC.PCFX.Tst");
            pcfxSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            var portDevices = pcfxSync.GetOrCreateContainer("PortDevices");
            
            for (int i = 0; i < 8; i++)
            {
                portDevices[i.ToString()] = "gamepad";
            }
        }

        private void ConfigureNymashock(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Nymashock")
                return;

            var nymashockSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Sony.PSX.Nymashock");
            nymashockSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";

            var mednafenValues = nymashockSync.GetOrCreateContainer("MednafenValues");
            if (Controllers.Count > 5)
            {
                mednafenValues["psx.input.pport1.multitap"] = "1";
                mednafenValues["psx.input.pport1.multitap"] = "1";
            }
            else if (Controllers.Count > 2)
            {
                mednafenValues["psx.input.pport1.multitap"] = "0";
                mednafenValues["psx.input.pport1.multitap"] = "1";
            }
            else
            {
                mednafenValues["psx.input.pport1.multitap"] = "0";
                mednafenValues["psx.input.pport1.multitap"] = "0";
            }

            if (SystemConfig.isOptSet("bizhawk_psx_region") && !string.IsNullOrEmpty(SystemConfig["bizhawk_psx_region"]))
            {
                mednafenValues["psx.region_default"] = SystemConfig["bizhawk_psx_region"];
                mednafenValues["psx.region_autodetect"] = "0";
            }
            else
            {
                mednafenValues["psx.region_default"] = "jp";
                mednafenValues["psx.region_autodetect"] = "1";
            }

            var portDevices = nymashockSync.GetOrCreateContainer("PortDevices");

            if (SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
            {
                for (int i = 0; i < 8; i++)
                    portDevices[i.ToString()] = "gamepad";
            }
            else
            {
                for (int i = 0; i < 8; i++)
                    portDevices[i.ToString()] = "dualshock";
            }

            if (SystemConfig.isOptSet("bizhawk_psx_mouse") && !string.IsNullOrEmpty(SystemConfig["bizhawk_psx_mouse"]))
            {
                string mouseInfo = SystemConfig["bizhawk_psx_mouse"];
                switch (mouseInfo)
                {
                    case "1":
                        portDevices["0"] = "mouse";
                        break;
                    case "2":
                        portDevices["1"] = "mouse";
                        break;
                    case "both":
                        portDevices["0"] = "mouse";
                        portDevices["1"] = "mouse";
                        break;
                }
            }
        }

        private void ConfigureOctoshock(DynamicJson coreSettings, DynamicJson coreSyncSettings, string core)
        {
            if (core != "Octoshock")
                return;

            var octoshockSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Sony.PSX.Octoshock");
            octoshockSync["$type"] = "BizHawk.Emulation.Cores.Sony.PSX.Octoshock+SyncSettings, BizHawk.Emulation.Cores";

            var fioconfig = octoshockSync.GetOrCreateContainer("FIOConfig");
            fioconfig.Remove("Multitaps");

            // Add memcards
            List<bool> memcards = new List<bool>
            {
                true,
                true
            };
            fioconfig.SetObject("Memcards", memcards);

            // Multitaps
            List<bool> multitaps = new List<bool>();

            if (Controllers.Count > 5)
            {
                multitaps.Add(true);
                multitaps.Add(true);
            }
            else if (Controllers.Count > 2)
            {
                multitaps.Add(false);
                multitaps.Add(true);
            }
            else
            {
                multitaps.Add(false);
                multitaps.Add(false);
            }
            fioconfig.SetObject("Multitaps", multitaps);

            // Controller devices
            List<int> deviceTypes = new List<int>();
            if (SystemConfig.isOptSet("bizhawk_psx_digital") && SystemConfig.getOptBoolean("bizhawk_psx_digital"))
            {
                for (int i = 0; i < 8; i++)
                    deviceTypes.Add(1);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                    deviceTypes.Add(2);
            }
            fioconfig.SetObject("Devices8", deviceTypes);

            var octoshock = coreSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Sony.PSX.Octoshock");
            octoshock["$type"] = "BizHawk.Emulation.Cores.Sony.PSX.Octoshock+Settings, BizHawk.Emulation.Cores";
            octoshock["ResolutionMode"] = "3";
        }

        private void ConfigureO2Hawk(DynamicJson coreSyncSettings, string core)
        {
            if (core != "O2Hawk")
                return;

            var o2Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.O2Hawk.O2Hawk");
            o2Sync["$type"] = "BizHawk.Emulation.Cores.Consoles.O2Hawk.O2Hawk+O2SyncSettings, BizHawk.Emulation.Cores";

            BindBoolFeature(o2Sync, "G7400_Enable", "bizhawk_o2_g7400", "true", "false");
        }

        private void Configuretic80(DynamicJson coreSyncSettings, string core)
        {
            if (core != "TIC-80")
                return;

            var tic80Sync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Computers.TIC80.TIC80");
            tic80Sync["$type"] = "BizHawk.Emulation.Cores.Computers.TIC80.TIC80+TIC80SyncSettings, BizHawk.Emulation.Cores";

            for (int i = 1; i < 5; i++)
                tic80Sync["Gamepad" + i] = "true";

            tic80Sync["Mouse"] = "true";
        }

        private void ConfigureticVirtualBoyee(DynamicJson coreSyncSettings, string core)
        {
            if (core != "VirtualBoyee")
                return;

            var vbSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Consoles.Nintendo.VB.VirtualBoyee");
            vbSync["$type"] = "BizHawk.Emulation.Cores.Waterbox.NymaCore+NymaSyncSettings, BizHawk.Emulation.Cores";
        }

        private void ConfigureCygne(DynamicJson coreSyncSettings, string core)
        {
            if (core != "Cygne")
                return;

            var wswanSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.WonderSwan.WonderSwan");
            wswanSync["$type"] = "BizHawk.Emulation.Cores.WonderSwan.WonderSwan+SyncSettings, BizHawk.Emulation.Cores";

            BindFeature(wswanSync, "Language", "bizhawk_wswan_language", "1");
        }

        private void ConfigureZXHawk(DynamicJson coreSyncSettings, string core)
        {
            if (core != "ZXHawk")
                return;

            var zxSync = coreSyncSettings.GetOrCreateContainer("BizHawk.Emulation.Cores.Computers.SinclairSpectrum.ZXSpectrum");
            zxSync["$type"] = "BizHawk.Emulation.Cores.Computers.SinclairSpectrum.ZXSpectrum+ZXSpectrumSyncSettings, BizHawk.Emulation.Cores";
            zxSync["JoystickType1"] = "1";
            zxSync["JoystickType2"] = "2";
            zxSync["JoystickType3"] = "3";

            BindFeature(zxSync, "MachineType", "bizhawk_zx_machine", "1");
        }
    }
}
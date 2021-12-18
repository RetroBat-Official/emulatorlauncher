﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using System.IO;
using System.Drawing;

namespace emulatorLauncher
{
    abstract class Generator
    {
        protected void SetCustomError(string message)
        {
            try
            {
                ExitCode = ExitCodes.CustomError;
                Program.WriteCustomErrorFile(message);
            }
            catch 
            { 
                
            }
        }

        public ExitCodes ExitCode { get; protected set; }

        public Generator()
        {
            UsePadToKey = true;
            DependsOnDesktopResolution = false;
            ExitCode = ExitCodes.EmulatorNotInstalled;
        }

        protected EsFeatures Features { get { return Program.Features; } }
        protected ConfigFile AppConfig { get { return Program.AppConfig; } }
        protected ConfigFile SystemConfig { get { return Program.SystemConfig; } }
        protected List<Controller> Controllers { get { return Program.Controllers; } }

        public abstract ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution);
        public virtual void Cleanup() { }

        public virtual int RunAndWait(ProcessStartInfo path)
        {
            try 
            {
                var process = Process.Start(path);
                process.WaitForExit();
                                 
                int exitCode = process.ExitCode;

                if (exitCode == unchecked((int)0xc0000005)) // Null pointer - happen sometimes with Yuzu
                    return 0;

                if (exitCode == unchecked((int)0xc0000374)) // Heap corruption - happen sometimes with scummvm
                    return 0;
                
                return process.ExitCode;
            }
            catch 
            { 

            }

            return -1;
        }

        public bool DependsOnDesktopResolution { get; protected set; }
        public bool UsePadToKey { get; protected set; }

        public virtual PadToKey SetupCustomPadToKeyMapping(PadToKeyboard.PadToKey mapping)
        {
            return mapping;
        }


        private Dictionary<string, byte[]> _filesToRestore;

        protected void AddFileForRestoration(string file)
        {
            if (_filesToRestore == null)
                _filesToRestore = new Dictionary<string, byte[]>();

            if (File.Exists(file))
            {
                try { _filesToRestore[file] = File.ReadAllBytes(file); }
                catch { }
            }
        }

        public void RestoreFiles()
        {
            if (_filesToRestore == null)
                return;

            foreach (var file in _filesToRestore)
                File.WriteAllBytes(file.Key, file.Value);
        }

        #region IsEmulationStationWindowed
        static string GetParentProcessCommandline()
        {
            return GetParentProcessCommandline(Process.GetCurrentProcess());
        }

        static string GetParentProcessCommandline(Process process)
        {
            try
            {
                using (var query = new System.Management.ManagementObjectSearcher("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId=" + process.Id))
                {
                    var parentProcessId = query.Get()
                      .OfType<System.Management.ManagementObject>()
                      .Select(p => (int)(uint)p["ParentProcessId"])
                      .FirstOrDefault();

                    using (var cquery = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + parentProcessId))
                    {
                        var commandLine = cquery.Get()
                          .OfType<System.Management.ManagementObject>()
                          .Select(p => (string)p["CommandLine"])
                          .FirstOrDefault();

                        return commandLine;
                    }
                }
            }
            catch
            {

            }

            return null;
        }

        public static bool IsEmulationStationWindowed(out Size windowSize)
        {
            bool isWindowed = false;

            windowSize = new Size();

            // Check parent process is EmulationStation. Get its commandline, see if it's using "--windowed --resolution X Y", import settings
            var px = GetParentProcessCommandline();
            if (!string.IsNullOrEmpty(px) && px.IndexOf("emulationstation", StringComparison.InvariantCultureIgnoreCase) >= 0)
            {
                var args = Misc.SplitCommandLine(px).Skip(1).ToArray();
                for (int i = 0; i < args.Length; i++)
                {
                    var arg = args[i];

                    if (arg == "--windowed")
                        isWindowed = true;
                    else if (arg == "--resolution" && i + 2 < args.Length)
                        windowSize = new Size(args[i + 1].ToInteger(), args[i + 2].ToInteger());
                }
            }

            return isWindowed && windowSize.Width > 0 && windowSize.Height > 0;
        }
        #endregion
    }

    enum ExitCodes : int
    {
        OK = 0,
        EmulatorExitedUnexpectedly = 200,
        BadCommandLine = 201,
        InvalidConfiguration = 202,
        UnknownEmulator = 203,
        EmulatorNotInstalled = 204,
        MissingCore = 205,

        CustomError = 299
    }

}

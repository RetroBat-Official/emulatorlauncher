using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;
using emulatorLauncher.Tools;
using System.IO;

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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using emulatorLauncher.PadToKeyboard;

namespace emulatorLauncher
{
    abstract class Generator
    {
        public Generator()
        {
            UsePadToKey = true;
            DependsOnDesktopResolution = false;
        }

        protected ConfigFile AppConfig { get { return Program.AppConfig; } }
        protected ConfigFile SystemConfig { get { return Program.SystemConfig; } }
        protected List<Controller> Controllers { get { return Program.Controllers; } }

        public abstract ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution);
        public virtual void Cleanup() { }

        public virtual void RunAndWait(ProcessStartInfo path)
        {
            try { Process.Start(path).WaitForExit(); }
            catch { }
        }

        public bool DependsOnDesktopResolution { get; protected set; }
        public bool UsePadToKey { get; protected set; }

        public virtual PadToKey SetupCustomPadToKeyMapping(PadToKeyboard.PadToKey mapping)
        {
            return mapping;
        }
    }
}

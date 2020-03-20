using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace emulatorLauncher
{
    abstract class Generator
    {
        protected ConfigFile AppConfig { get { return Program.AppConfig; } }
        protected ConfigFile SystemConfig { get { return Program.SystemConfig; } }

        public abstract ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, string gameResolution);
        public virtual void Cleanup() { }
    }
}

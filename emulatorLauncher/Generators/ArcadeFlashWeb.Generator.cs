using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using System;
using System.Windows.Forms;
using System.Threading;
using System.Windows.Interop;

namespace EmulatorLauncher
{
    class ArcadeFlashWebGenerator : Generator
    {
        public ArcadeFlashWebGenerator()
        {
            DependsOnDesktopResolution = false;
        }

        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {            
            string path = AppConfig.GetFullPath("ArcadeFlashWeb");

            string exe = Path.Combine(path, "ArcadeFlashWeb.exe");
            if (!File.Exists(exe))
                return null;

            bool fullscreen = !IsEmulationStationWindowed() || SystemConfig.getOptBoolean("forcefullscreen");

            List<string> commandArray = new List<string>();

            if (fullscreen)
                commandArray.Add("-fullscreen");

            commandArray.Add("-nodatetime");
            commandArray.Add("-source:\"" + rom + "\"");

            if (!string.IsNullOrEmpty(AppConfig["saves"]) && Directory.Exists(AppConfig.GetFullPath("saves")))
            {
                string savePath = Path.Combine(AppConfig.GetFullPath("saves"), system);
                if (!Directory.Exists(savePath)) try { Directory.CreateDirectory(savePath); }
                    catch { }

                commandArray.Add("-savedataflash:\"" + savePath + "\"");
            }

            if (!string.IsNullOrEmpty(AppConfig["screenshots"]) && Directory.Exists(AppConfig.GetFullPath("screenshots")))
                commandArray.Add("-picturesfolder:\"" + AppConfig.GetFullPath("screenshots") + "\"");

            string args = string.Join(" ", commandArray);

            var ret = new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,
                Arguments = args,
            };

            return ret;
        }

        public override int RunAndWait(ProcessStartInfo path)
        {
            Process process = Process.Start(path);
            Thread.Sleep(3000);

            var hWnd = User32.FindHwnd(process.Id);
            var focusApp = User32.GetForegroundWindow();

            while (hWnd != focusApp)
            {
                var name = User32.GetWindowText(focusApp);
                var name2 = User32.GetWindowText(hWnd);
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                User32.SetForegroundWindow(hWnd);
                break;
            }

            if (process != null)
            {
                process.WaitForExit();

                try { return process.ExitCode; }
                catch { }
            }

            return 0;
        }
    }
}

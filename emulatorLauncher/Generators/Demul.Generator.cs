using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using emulatorLauncher.PadToKeyboard;
using System.Windows.Forms;
using System.Threading;

namespace emulatorLauncher
{
    class DemulGenerator : Generator
    {
        public override System.Diagnostics.ProcessStartInfo Generate(string system, string emulator, string core, string rom, string playersControllers, ScreenResolution resolution)
        {
            string folderName = (emulator == "demul-old" || core == "demul-old") ? "demul-old" : "demul";

            string path = AppConfig.GetFullPath(folderName);
            if (string.IsNullOrEmpty(path))
                path = AppConfig.GetFullPath("demul");

            string exe = Path.Combine(path, "demul.exe");
            if (!File.Exists(exe))
                return null;

            SetupGeneralConfig(path, rom, system);
            SetupDx11Config(path, rom, system);

            string demulCore = "dreamcast";

            if (emulator == "demul-hikaru" || core == "hikaru")
                demulCore = "hikaru";
            else if (emulator == "demul-gaelco" || core == "hikaru")
                demulCore = "gaelco";
            else if (emulator == "demul-atomiswave" || core == "atomiswave")
                demulCore = "awave";
            else if (emulator == "demul-naomi" || emulator == "demul-naomi2" || core == "naomi")
                demulCore = "naomi";
            else
            {
                switch (system)
                {
                    case "hikaru":
                        demulCore = "hikaru"; break;
                    case "gaelco":
                        demulCore = "gaelco"; break;
                    case "naomi":
                    case "naomi2":
                        demulCore = "naomi"; break;
                    case "atomiswave":
                        demulCore = "awave"; break;
                }
            }

            return new ProcessStartInfo()
            {
                FileName = exe,
                WorkingDirectory = path,                
                Arguments = "-run=" + demulCore + " -rom=\"" + Path.GetFileNameWithoutExtension(rom).ToLower() + "\"",
            };
        }

        public override void RunAndWait(ProcessStartInfo path)
        {
            var process = Process.Start(path);

            while (process != null)
            {
                if (process.WaitForExit(50))
                {
                    process = null;
                    break;
                }

                var hWnd = FindHwnd(process.Id);
                if (hWnd == IntPtr.Zero)
                    continue;
                
                var name = GetWindowText(hWnd);
                if (name != null && name.StartsWith("gpu"))
                {                    
                    SendKeys.SendWait("%~");
                    break;
                }
            }

            if (process != null)
                process.WaitForExit();
        }

        #region Apis
        private IntPtr FindHwnd(int processId)
        {
            IntPtr hWnd = GetWindow(GetDesktopWindow(), GW.CHILD);
            while (hWnd != IntPtr.Zero)
            {
                if (IsWindowVisible(hWnd))
                {
                    uint wndProcessId;
                    GetWindowThreadProcessId(hWnd, out wndProcessId);
                    if (wndProcessId == processId)
                        return hWnd;
                }

                hWnd = GetWindow(hWnd, GW.HWNDNEXT);
            }

            return IntPtr.Zero;
        }

        [DllImport("user32.dll", SetLastError = false)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("User32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetWindow(IntPtr hWnd, GW cmd);

        public enum GW : int
        {
            HWNDFIRST = 0,
            HWNDLAST = 1,
            HWNDNEXT = 2,
            HWNDPREV = 3,
            OWNER = 4,
            CHILD = 5
        }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        public static string GetWindowText(IntPtr hWnd)
        {
            int capacity = GetWindowTextLength(hWnd) * 2;
            StringBuilder lpString = new StringBuilder(capacity);
            GetWindowText(hWnd, lpString, lpString.Capacity);
            return lpString.ToString();
        }
        #endregion

        private void SetupGeneralConfig(string path, string rom, string system)
        {
            string iniFile = Path.Combine(path, "Demul.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("files", "roms0", AppConfig.GetFullPath("bios"));
                    ini.WriteValue("files", "roms1", Path.GetDirectoryName(rom));
                    ini.WriteValue("files", "romsPathsCount", "2");

                    ini.WriteValue("plugins", "directory", @".\plugins\");
                    ini.WriteValue("plugins", "gpu", "gpuDX11.dll");
                    ini.WriteValue("plugins", "pad", "padDemul.dll");

                    if (ini.GetValue("plugins", "gpu") == null)
                        ini.WriteValue("plugins", "gdr", "gdrCHD.dll");

                    if (ini.GetValue("plugins", "spu") == null)
                        ini.WriteValue("plugins", "spu", "spuDemul.dll");

                    if (ini.GetValue("plugins", "net") == null)
                        ini.WriteValue("plugins", "net", "netDemul.dll");
                }
            }

            catch { }
        }

        private void SetupDx11Config(string path, string rom, string system)
        {
            string iniFile = Path.Combine(path, "gpuDX11.ini");
            if (!File.Exists(iniFile))
                return;

            try
            {
                using (var ini = new IniFile(iniFile, true))
                {
                    ini.WriteValue("main", "UseFullscreen", "0");
                    ini.WriteValue("main", "Vsync", SystemConfig["VSync"] != "false" ? "1" : "0");                    
                }
            }

            catch { }
        }
    }
}

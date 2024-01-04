using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using EmulatorLauncher.Common.FileFormats;
using System.IO;
using EmulatorLauncher.Common;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;

namespace batocera_services
{
    static class Program
    {
        public const string SECOND_MONITOR_MARQUEE = "SECOND MONITOR MARQUEE";

        private static string GetAutoMarqueePath()
        {
            return Path.Combine(Path.GetDirectoryName(typeof(Program).Assembly.Location), ".emulationstation", "scripts", "automarquee.bat");
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            if (args[0] == "list")
            {
                ListServices(args);
                return;
            }

            if (args.Length < 2)
                return;

            if (args[0] == "enable" || args[0] == "disable" || args[0] == "start" || args[0] == "stop")
            {
                EnableServices(args);
                return;
            }
               
            if (args[0] == "marquee")
            {                
                PlaySecondMonitorMarquee(args);
                return;
            }
        }

        private static void ListServices(string[] args)
        {
            if (Screen.AllScreens.Length > 1)
                Console.WriteLine(SECOND_MONITOR_MARQUEE + ";" + (File.Exists(GetAutoMarqueePath()) ? "*" : "-"));
        }
        
        private static void EnableServices(string[] args)
        {
            var state = args[0];
            var serviceName = args[1];

            if (serviceName == SECOND_MONITOR_MARQUEE)
            {
                if (state == "enable")
                {
                    string batContent = "@ECHO OFF\r\n\"" + Path.GetFullPath(typeof(Program).Assembly.Location) + "\" marquee %1 %2 %3";
                    File.WriteAllText(GetAutoMarqueePath(), batContent);
                  
                    var psi = new ProcessStartInfo();
                    psi.FileName = typeof(Program).Assembly.Location;
                    psi.Arguments = "marquee start";
                    psi.UseShellExecute = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    Process.Start(psi);                 
                }
                else if (state == "disable")
                {
                    if (NamedPipeServer.IsServerRunning())
                        NamedPipeClient.SendArguments(new string[] { "quit" });

                    FileTools.TryDeleteFile(GetAutoMarqueePath());
                }
            }
        }

        private static void PlaySecondMonitorMarquee(string[] args)
        {
            SimpleLogger.Instance.Debug(string.Join(" ", args));

            string eventName = args[1];
            string arg1 = args.Length > 2 ? args[2] : "";
            string arg2 = args.Length > 3 ? args[3] : "";
            string arg3 = args.Length > 4 ? args[4] : "";

            if (eventName == "start")
            {                
                if (NamedPipeServer.IsServerRunning())
                    return;

                SetBrowserFeaturesToSupportGpu();

                SecondScreenMarqueeFrm frm = new SecondScreenMarqueeFrm();
                Application.Run(frm);
                return;
            }

            if (NamedPipeServer.IsServerRunning())
                NamedPipeClient.SendArguments(args.Skip(1).ToArray());
        }

        public static void SetBrowserFeaturesToSupportGpu()
        {
            // FeatureControl settings are per-process
            var fileName = System.IO.Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);

            SetBrowserFeatureControlKey("FEATURE_96DPI_PIXEL", fileName, 1); // enable hi-dpi support
            SetBrowserFeatureControlKey("FEATURE_AJAX_CONNECTIONEVENTS", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_ENABLE_CLIPCHILDREN_OPTIMIZATION", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_GPU_RENDERING", fileName, 1); // use GPU rendering
            SetBrowserFeatureControlKey("FEATURE_IVIEWOBJECTDRAW_DMLT9_WITH_GDI", fileName, 0); // force directX
            SetBrowserFeatureControlKey("FEATURE_NINPUT_LEGACYMODE", fileName, 0);
            SetBrowserFeatureControlKey("FEATURE_DISABLE_NAVIGATION_SOUNDS", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_SCRIPTURL_MITIGATION", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_SPELLCHECKING", fileName, 0);
            SetBrowserFeatureControlKey("FEATURE_STATUS_BAR_THROTTLING", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_VALIDATE_NAVIGATE_URL", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_WEBOC_DOCUMENT_ZOOM", fileName, 1); // allow zoom.
            SetBrowserFeatureControlKey("FEATURE_WEBOC_POPUPMANAGEMENT", fileName, 0); // disallow auto-popups
            SetBrowserFeatureControlKey("FEATURE_ADDON_MANAGEMENT", fileName, 0);       // disallow auto-addons/plugins
            SetBrowserFeatureControlKey("FEATURE_WEBSOCKET", fileName, 1);
            SetBrowserFeatureControlKey("FEATURE_WINDOW_RESTRICTIONS", fileName, 0); // disallow popups
        }

        private static void SetBrowserFeatureControlKey(string feature, string appName, uint value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(
                String.Concat(@"Software\Microsoft\Internet Explorer\Main\FeatureControl\", feature),
                RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                key.SetValue(appName, (UInt32)value, RegistryValueKind.DWord);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;

namespace EmulatorLauncher.Common
{
    public static class ProcessExtensions
    {
        public static int[] GetChildrenProcessIds(this Process process)
        {
            return GetChildrenProcessIds(process.Id);
        }
         
        public static int[] GetChildrenProcessIds(int pid)
        {
            var ret = new List<int>();

            var searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            var moc = searcher.Get();

            foreach (var mo in moc)
            {
                using (mo)
                {
                    try
                    {
                        var processId = Convert.ToInt32(mo["ProcessID"]);
                        if (processId > 0)
                        {
                            ret.Add(processId);

                            foreach (var child in GetChildrenProcessIds(processId))
                                ret.Add(child);
                        }
                    }
                    catch { }
                }
            }

            return ret.ToArray();
        }

        public static string GetProcessCommandline(this Process process)
        {
            if (process == null)
                return null;

            try
            {
                using (var cquery = new System.Management.ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId=" + process.Id))
                {
                    var commandLine = cquery.Get()
                        .OfType<System.Management.ManagementObject>()
                        .Select(p => (string)p["CommandLine"])
                        .FirstOrDefault();

                    return commandLine;
                }
            }
            catch
            {

            }

            return null;
        }

        /// <summary>
        /// Get the full path of the executable of a process (also works on a 64-bit process from a 32-bit application)
        /// </summary>
        public static string GetProcessPath(this Process process)
        {
            if (process == null)
                return null;

            IntPtr handle = IntPtr.Zero;

            try
            {
                handle = Kernel32.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, (uint)process.Id);
                if (handle == IntPtr.Zero)
                    return null;

                int size = 1024;
                var path = new StringBuilder(size);

                if (Kernel32.QueryFullProcessImageName(handle, 0, path, ref size))
                    return path.ToString();
            }
            catch { }
            finally
            {
                if (handle != IntPtr.Zero)
                    Kernel32.CloseHandle(handle);
            }

            return null;
        }

        public static string RunWithOutput(string fileName, string arguments = null)
        {
            var ps = new ProcessStartInfo() { FileName = fileName };
            if (arguments != null)
                ps.Arguments = arguments;

            return RunWithOutput(ps);
        }

        public static string RunWithOutput(this ProcessStartInfo ps)
        {
            List<string> lines = new List<string>();

            ps.UseShellExecute = false;
            ps.RedirectStandardOutput = true;
            ps.RedirectStandardError = true;
            ps.CreateNoWindow = true;

            var proc = new Process();
            proc.StartInfo = ps;
            proc.Start();

            string output = proc.StandardOutput.ReadToEnd();
            string err = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            return (err ?? "") + (output ?? "");
        }

    }
}

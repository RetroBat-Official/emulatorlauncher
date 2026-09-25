using EmulatorLauncher.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.RegularExpressions;

namespace EmulatorLauncher
{
    /// <summary>
    /// Extracts the PinMAME rom name (cGameName) from a Visual Pinball X table (.vpx).
    /// Reads the sidecar .vbs if present (VPX gives it priority), otherwise the script embedded in the table.
    /// </summary>
    static class VpxRomNameReader
    {
        /// <summary>
        /// Returns the rom name used by the table, or null if none could be found (original/EM table, protected or unreadable script).
        /// When romsPath is provided, the first candidate whose zip exists in that folder is preferred.
        /// </summary>
        public static string GetRomName(string vpxPath, string romsPath = null)
        {
            if (string.IsNullOrEmpty(vpxPath) || !File.Exists(vpxPath))
                return null;

            string script = null;

            // A sidecar .vbs overrides the embedded script in VPX, so it takes priority here as well
            string sidecar = Path.ChangeExtension(vpxPath, ".vbs");
            if (File.Exists(sidecar))
            {
                try { script = File.ReadAllText(sidecar, Encoding.Default); }
                catch { }
            }

            if (string.IsNullOrEmpty(script))
                script = ReadEmbeddedScript(vpxPath);

            if (string.IsNullOrEmpty(script))
                return null;

            var candidates = ExtractCandidates(script);
            if (candidates.Count == 0)
                return null;

            // Prefer a candidate for which a rom zip actually exists
            if (!string.IsNullOrEmpty(romsPath) && Directory.Exists(romsPath))
            {
                foreach (var name in candidates)
                {
                    if (File.Exists(Path.Combine(romsPath, name + ".zip")))
                        return name;
                }
            }

            return candidates[0];
        }

        #region Script parsing

        // Ordered by priority: a lower priority pattern is only used when higher ones found nothing
        private static readonly Regex[] RomPatterns = new Regex[]
        {
            // Const cGameName = "afm_113b"  /  cGameName="afm_113b"
            new Regex(@"\bcGameName\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase),
            // Controller.GameName = "afm_113b"  /  .GameName = "afm_113b"
            new Regex(@"\.GameName\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase),
            // GameName = "afm_113b" (generic fallback)
            new Regex(@"\bGameName\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase),
        };

        private static List<string> ExtractCandidates(string script)
        {
            var result = new List<string>();
            var lines = script.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None)
                              .Select(l => StripComment(l))
                              .Where(l => l.Length > 0)
                              .ToArray();

            foreach (var regex in RomPatterns)
            {
                foreach (var line in lines)
                {
                    foreach (Match m in regex.Matches(line))
                    {
                        string name = m.Groups[1].Value.Trim();
                        if (name.Length > 0 && !result.Contains(name, StringComparer.OrdinalIgnoreCase))
                            result.Add(name);
                    }
                }

                if (result.Count > 0)
                    break;
            }

            return result;
        }

        // Removes VBScript comments (' and Rem), ignoring quotes inside string literals
        private static string StripComment(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.Equals("rem", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("rem ", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("rem\t", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            bool inString = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '"')
                    inString = !inString;
                else if (line[i] == '\'' && !inString)
                    return line.Substring(0, i);
            }

            return line;
        }

        #endregion

        #region Compound file reading (ole32)

        private const uint STGM_READ = 0x00000000;
        private const uint STGM_SHARE_EXCLUSIVE = 0x00000010;
        private const uint STGM_SHARE_DENY_WRITE = 0x00000020;
        private const int STATFLAG_NONAME = 1;

        [ComImport, Guid("0000000b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IStorage
        {
            // Only the first vtable entries are declared; trailing methods are not needed
            void CreateStream([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStream ppstm);
            void OpenStream([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, IntPtr reserved1, uint grfMode, uint reserved2, out IStream ppstm);
            void CreateStorage([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, uint grfMode, uint reserved1, uint reserved2, out IStorage ppstg);
            void OpenStorage([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, IStorage pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstg);
        }

        [DllImport("ole32.dll")]
        private static extern int StgOpenStorage([MarshalAs(UnmanagedType.LPWStr)] string pwcsName, IStorage pstgPriority, uint grfMode, IntPtr snbExclude, uint reserved, out IStorage ppstgOpen);

        private static string ReadEmbeddedScript(string vpxPath)
        {
            IStorage root = null;
            IStorage gameStg = null;
            IStream stream = null;

            try
            {
                if (StgOpenStorage(vpxPath, null, STGM_READ | STGM_SHARE_DENY_WRITE, IntPtr.Zero, 0, out root) != 0 || root == null)
                    return null;

                root.OpenStorage("GameStg", null, STGM_READ | STGM_SHARE_EXCLUSIVE, IntPtr.Zero, 0, out gameStg);
                if (gameStg == null)
                    return null;

                gameStg.OpenStream("GameData", IntPtr.Zero, STGM_READ | STGM_SHARE_EXCLUSIVE, 0, out stream);
                if (stream == null)
                    return null;

                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                stream.Stat(out stat, STATFLAG_NONAME);
                if (stat.cbSize <= 0 || stat.cbSize > int.MaxValue)
                    return null;

                byte[] data = new byte[stat.cbSize];
                stream.Read(data, data.Length, IntPtr.Zero);

                return FindCodeRecord(data);
            }
            catch (Exception ex)
            {
                // Locked file, protected table or unexpected format
                SimpleLogger.Instance.Warning("[VpxRomNameReader] Unable to read table script: " + ex.Message);
                return null;
            }
            finally
            {
                if (stream != null) Marshal.ReleaseComObject(stream);
                if (gameStg != null) Marshal.ReleaseComObject(gameStg);
                if (root != null) Marshal.ReleaseComObject(root);
            }
        }

        // BIFF layout: [int32 length][4 char tag][data], length includes the tag.
        // Exception: for CODE, length = 4 (tag only) and is followed by [int32 scriptLength][script bytes].
        private static string FindCodeRecord(byte[] data)
        {
            int pos = 0;
            while (pos + 8 <= data.Length)
            {
                int len = BitConverter.ToInt32(data, pos);
                string tag = Encoding.ASCII.GetString(data, pos + 4, 4);

                if (tag == "CODE")
                {
                    if (pos + 12 > data.Length)
                        return null;

                    int codeLen = BitConverter.ToInt32(data, pos + 8);
                    if (codeLen <= 0 || pos + 12 + codeLen > data.Length)
                        return null;

                    return Encoding.Default.GetString(data, pos + 12, codeLen);
                }

                if (tag == "ENDB" || len < 4)
                    break;

                pos += 4 + len;
            }

            return null;
        }

        #endregion
    }
}
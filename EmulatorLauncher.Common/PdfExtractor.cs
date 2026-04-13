using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace EmulatorLauncher.Common
{
    public class PdfExtractor
    {
        private static Dictionary<string, string> _cache = new Dictionary<string, string>();

        public static void ClearCache()
        {
            foreach (var folder in _cache.Values)
            {
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch { }
            }
            _cache.Clear();
        }

        public static string BeginExtractPdfPages(string pdfPath, int quality = 300, Action<int, int> onPageReady = null)
        {
            // Return cached folder if already extracted
            if (_cache.TryGetValue(pdfPath, out string existing)
                && Directory.Exists(existing)
                && Directory.GetFiles(existing, "*.ppm").Length > 0)
            {
                int available = Directory.GetFiles(existing, "*.ppm").Length;
                Task.Factory.StartNew(() => onPageReady?.Invoke(available, available));
                return existing;
            }

            int pages = GetPdfPageCount(pdfPath);
            if (pages == 0)
                return null;

            string pdfFolder = Path.Combine(Path.GetTempPath(), "pdftmp_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(pdfFolder);
            _cache[pdfPath] = pdfFolder;

            // Extract page 1 synchronously so viewer can open immediately
            ExtractPdfPage(pdfPath, 1, 1, quality, pdfFolder);
            onPageReady?.Invoke(1, pages);

            // Extract remaining pages in background
            Task.Factory.StartNew(() =>
            {
                const int batch = 2;
                for (int i = 2; i <= pages; i += batch)
                {
                    int count = Math.Min(batch, pages - i + 1);
                    ExtractPdfPage(pdfPath, i, count, quality, pdfFolder);
                    onPageReady?.Invoke(i + count - 1, pages);
                }
            });

            return pdfFolder;
        }

        public static int GetPdfPageCount(string pdfPath)
        {
            if (!File.Exists(pdfPath))
                return 0;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pdfinfo.exe",
                    Arguments = $"\"{pdfPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return 0;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                        return 0;

                    using (var sr = new StringReader(output))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (line.StartsWith("Pages:", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Substring(6).Trim();
                                if (int.TryParse(parts, NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
                                    return count;
                            }
                        }
                    }
                }
            }
            catch { }

            return 0;
        }

        private static void ExtractPdfPage(string pdfPath, int pageIndex, int pageCount, int quality, string outputFolder)
        {
            string prefix = Path.GetFileNameWithoutExtension(pdfPath)
                            + "-" + quality
                            + "-" + pageIndex.ToString("D8")
                            + "-pdf";

            var psi = new ProcessStartInfo
            {
                FileName = "pdftoppm.exe",
                Arguments = $"-r {quality} -f {pageIndex} -l {pageIndex + pageCount - 1} \"{pdfPath}\" \"{outputFolder}/{prefix}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
                process.WaitForExit();
        }
    }
}
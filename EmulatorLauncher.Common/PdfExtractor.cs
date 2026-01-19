using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EmulatorLauncher.Common
{
    public class PdfExtractor
    {
        public static string ExtractPdfPages(string pdfPath, int quality = 125)
        {
            string pdfFolder = Path.Combine(Path.GetTempPath(), "pdftmp");

            try
            {
                if (Directory.Exists(pdfFolder))
                    Directory.Delete(pdfFolder, true);

                Directory.CreateDirectory(pdfFolder);
            }
            catch { }

            int numberOfPagesToProcess = 2;
            var pages = GetPdfPageCount(pdfPath);
            if (pages == 0)
                return null;

            var tasks = new List<Task>();

            for (int i = 0; i <= pages; i += numberOfPagesToProcess)
            {
                int startPage = i;
                tasks.Add(Task.Factory.StartNew(() => ExtractPdfPage(pdfPath, startPage, numberOfPagesToProcess, quality, false)));
            }

            Task.WaitAll(tasks.ToArray());
            return pdfFolder;
        }

        public static string ExtractPdfPage(string pdfPath, int pageIndex, int pageCount = 1, int quality = 125, bool resetDirectory = true)
        {
            if (pageIndex < 0)
                return null;

            string pdfFolder = Path.Combine(Path.GetTempPath(), "pdftmp");

            if (resetDirectory)
            {
                try
                {
                    if (Directory.Exists(pdfFolder))
                        Directory.Delete(pdfFolder, true);

                    Directory.CreateDirectory(pdfFolder);
                }
                catch { }
            }

            string squality = quality.ToString();
            string prefix = "extract";
            string page = "";

            if (pageIndex >= 0)
            {
                string buffer = pageIndex.ToString("D8");

                if (pageIndex < 0)
                    prefix = "page-" + squality + "-" + buffer + "-pdf"; // page
                else
                    prefix = Path.GetFileNameWithoutExtension(pdfPath) + "-" + squality + "-" + buffer + "-pdf"; // page

                page = " -f " + pageIndex.ToString() + " -l " + (pageIndex + pageCount - 1).ToString();
            }

            var psi = new ProcessStartInfo
            {
                FileName = "pdftoppm.exe",
                Arguments = "-r " + squality + page + " \"" + pdfPath + "\" \"" + pdfFolder + "/" + prefix + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(psi))
                process.WaitForExit();

            return pdfFolder;
        }

        public static int GetPdfPageCount(string pdfPath)
        {
            if (!File.Exists(pdfPath))
                return 0;

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
                    throw new InvalidOperationException("Failed to start pdfinfo");

                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                    throw new Exception("pdfinfo error: " + error);

                using (var sr = new StringReader(output))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("Pages:", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = line.Substring(6).Trim();
                            if (int.TryParse(parts, NumberStyles.Integer, CultureInfo.InvariantCulture, out int pages))
                                return pages;
                        }
                    }
                }
            }

            throw new Exception("Pages info not found in pdfinfo output");
        }
        /*
        int getPdfPageCount(string fileName)
		{
			var lines = executeEnumerationScript("pdfinfo \"" + fileName + "\"");
			for (var line in lines)
			{
				auto splits = Utils::String::split(line, ':', true);
				if (splits.size() == 2 && splits[0] == "Pages")
					return atoi(Utils::String::trim(splits[1]).c_str());
			}

			return 0;
		}*/
    }
}

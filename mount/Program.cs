using System;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using emulatorLauncher;
using System.Threading;
using System.Collections.Generic;
using DokanNet;

namespace Mount
{
    class DokanMount
    {
        static void Test()
        {
            /*
            string archive = @"H:\[Emulz]\roms\windows\Monster Bash HD.wsquashfs";
            string destination = @"C:\Users\Fab\AppData\Local\Temp\.mountfs\Monster Bash HD.wsquashfs";
            string file = @"C:\Users\Fab\AppData\Local\Temp\.mountfs\Monster Bash HD.wsquashfs\drive_c/game/UnityPlayer.dll";
         
            string fileNameToExtract = @"drive_c/game/UnityPlayer.dll";

            try { File.Delete(file); }
            catch { }

            new Thread(() =>
            {
                 Zip.Extract(
                           archive,
                           destination,
                           fileNameToExtract, null, true);
             }).Start();

            while (!File.Exists(file))
                Thread.Sleep(1);

            var fs = new FileStream(file,
                System.IO.FileMode.Open,
                System.IO.FileAccess.Read,
                System.IO.FileShare.ReadWrite);*/
        }

        static int Main(string[] args)
        {
            // Check dokan is installed
            string dokan = Environment.GetEnvironmentVariable("DokanLibrary1");
            if (!Directory.Exists(dokan))
            {
                Console.WriteLine("Dokan 1.4.0 is required and is not installed");
                Process.Start("https://github.com/dokan-dev/dokany/releases/tag/v1.4.0.1000");
                return 1;
            }

            dokan = Path.Combine(dokan, "dokan1.dll");
            if (!File.Exists(dokan))
            {
                Console.WriteLine("Dokan 1.4.0 is required and is not installed");
                Process.Start("https://github.com/dokan-dev/dokany/releases/tag/v1.4.0.1000");
                return 1;
            }
            
            string driveLetter = null;
            string archiveFile = null;
            string overlayPath = null;
            string extractionPath = null;
            /*
#if DEBUG
            // -drive W -overlay . "H:\[Emulz]\roms\windows\Toki.wsquashfs" 
            driveLetter = "W";
            archiveFile = @"h:\xmoto.wsquashfs";
            archiveFile = @"H:\[Emulz]\roms\windows\Toki.wsquashfs";
            //archiveFile = @"h:\Pinball FX3.pc.zip";      
            //archiveFile = @"h:\scv.zip";                  
            overlayPath = @"."; // @".\.overlay";
#endif
            */
            var arguments = ParseArguments(args);

            if (arguments.ContainsKey("--help"))
            {
                Console.WriteLine("Usage : mount <filename> [-drive <drive letter>] [-overlay <overlay directory>] [-extractionpath <extraction directory>]");                
                return 1;
            }

            if (arguments.ContainsKey("archive"))
                archiveFile = arguments["archive"];

            if (arguments.ContainsKey("drive"))
                driveLetter = arguments["drive"];
            else if (arguments.ContainsKey("d"))
                driveLetter = arguments["d"];

            if (arguments.ContainsKey("overlay"))
                overlayPath = arguments["overlay"];
            else if (arguments.ContainsKey("o"))
                overlayPath = arguments["o"];

            if (arguments.ContainsKey("extractionpath"))
                extractionPath = arguments["extractionpath"];
            else if (arguments.ContainsKey("xp"))
                extractionPath = arguments["xp"];

            if (args.Any(a => a.Equals("-debug")))
                DokanOperations.DebugOutput = true;

            if (string.IsNullOrEmpty(driveLetter))
            {
                Console.WriteLine("-drive <letter> argument is missing");
                return 1;
            }

            if (string.IsNullOrEmpty(archiveFile))
            {
                Console.WriteLine("archive file is missing in arguments");
                return 1;
            }

            if (!Zip.IsCompressedFile(archiveFile))
            {
                Console.WriteLine("Unsupported format");
                return 1;
            }

            if (Path.GetExtension(archiveFile).ToLowerInvariant().Contains("squashfs"))
            {
                if (!File.Exists(Zip.GetRdSquashFSPath()))
                {
                    Console.WriteLine("rdsquashfs.exe is missing");
                    return 1;
                }
            }
            else if (!File.Exists(Zip.GetSevenZipPath()))
            {
                Console.WriteLine("7z.exe is missing");
                return 1;
            }

            if (driveLetter.Length == 1)
                driveLetter += ":";

            MountDrive(driveLetter, archiveFile, extractionPath, overlayPath);
            return 0;
            
            Task.Factory.StartNew(() => { MountDrive(driveLetter, archiveFile, extractionPath, overlayPath); });

            Thread.Sleep(500);
            Console.WriteLine(archiveFile + " mounted as " + driveLetter);

            var files = Directory.GetFiles(driveLetter, "*.exe");

            Console.WriteLine("Hit enter to unmount");
            Console.ReadLine();

            Dokan.Unmount(driveLetter[0]);
                      
            return 0;
        }

        private static void MountDrive(string driveLetter, string archiveFile, string extractionDirectory, string overlayPath)
        {
            try
            {
                var zip = new DokanOperations(archiveFile, extractionDirectory, overlayPath);

                DokanOptions options = DokanOptions.RemovableDrive;
                if (string.IsNullOrEmpty(overlayPath))
                    options |= DokanOptions.WriteProtection;
                /*
                if (DokanOperations.DebugOutput)
                    zip.Mount(driveLetter, options, Environment.ProcessorCount, 130, TimeSpan.FromMinutes(2), null);
                else*/
                    zip.Mount(driveLetter, options, Environment.ProcessorCount, 130, TimeSpan.FromMinutes(2), new DokanNet.Logging.NullLogger());
            }
            catch (DokanException ex)
            {
                Console.WriteLine(@"Error: " + ex.Message);
            }
        }

        private static Dictionary<string, string> ParseArguments(string[] args)
        {
            Dictionary<string, string> arguments = new Dictionary<string, string>();

            string current = null;
            foreach (string arg in args)
            {
                if (arg.StartsWith("-"))
                    current = arg.Substring(1);
                else if (arg.StartsWith("--"))
                    current = arg.Substring(2);
                else if (!string.IsNullOrEmpty(current))
                {
                    arguments[current] = arg;
                    current = null;
                }
                else
                    arguments["archive"] = arg;
            }

            return arguments;
        }
    }
}

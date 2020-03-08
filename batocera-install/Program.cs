using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;

namespace batocera_install
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 2 && args[0] == "install")
            {
                string device = args[1];
                string arch = args[2];

                return;
            }            

            if (args.Length > 0 && args[0] == "listArchs")
            {
                listArchs();
                return;
            }

            listDisks();            
        }

        static void listDisks()
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Removable && drive.DriveType != DriveType.Fixed)
                    continue;

                Console.WriteLine(drive.Name + " " + drive.Name +" " + drive.VolumeLabel ?? "DRIVE");
            }

        }

        static void listArchs()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;

            var req = WebRequest.Create("https://updates.batocera.org/installs.txt");

            var list = ReadResponseString(req.GetResponse()).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in list.OrderBy(l => l))
            {
                int idx = line.IndexOf("/");
                if (idx >= 0)
                    Console.WriteLine(line.Substring(0, idx));
                    
            }
        }

        public static string ReadResponseString(WebResponse response)
        {
            String responseString = null;

            using (Stream stream = response.GetResponseStream())
            {
                Encoding encoding = Encoding.Default;

                HttpWebResponse wr = response as HttpWebResponse;
                if (wr != null)
                    encoding = string.IsNullOrEmpty(wr.CharacterSet) ? Encoding.UTF8 : Encoding.GetEncoding(wr.CharacterSet);

                StreamReader reader = new StreamReader(stream, encoding);
                responseString = reader.ReadToEnd();
                stream.Close();

                if (!string.IsNullOrEmpty(responseString))
                    responseString = responseString.Trim();

                return responseString;
            }
        }

    }
}

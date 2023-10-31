using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.ComponentModel;
using System.Reflection;

namespace EmulatorLauncher.Common
{
    public static class WebTools
    {
        static WebTools()
        {
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            // Existe à partir de .net 4.5
            var tls11 = typeof(SecurityProtocolType).GetField("Tls11", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            if (tls11 != null)
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)tls11.GetValue(null);

            // Existe à partir de .net 4.5
            var tls12 = typeof(SecurityProtocolType).GetField("Tls12", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            if (tls12 != null)
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)tls12.GetValue(null);

            // Existe à partir de .net 4.8
            var tls13 = typeof(SecurityProtocolType).GetField("Tls13", BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public);
            if (tls13 != null)
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)tls13.GetValue(null);
        }

        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:95.0) Gecko/20100101 Firefox/95.0";
        private static Dictionary<string, bool> _urlExistsCache;

        public static bool UrlExists(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http"))
                return false;

            if (_urlExistsCache == null)
                _urlExistsCache = new Dictionary<string, bool>();
            else
            {
                bool exists = false;
                if (_urlExistsCache.TryGetValue(url, out exists))
                    return exists;
            }

            try
            {
                var req = WebRequest.Create(url) as HttpWebRequest;
                req.UserAgent = UserAgent;
                req.KeepAlive = false;
                req.Method = "HEAD";

                var resp = req.GetResponse() as HttpWebResponse;
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    resp.Close();
                    _urlExistsCache[url] = true;
                    return true;
                }

                resp.Close();
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[UrlExists] Exception " + ex.Message, ex);
            }

            _urlExistsCache[url] = false;
            return false;
        }

        public static string PostString(string url, string content)
        {
            var req = WebRequest.Create(url) as HttpWebRequest;
            req.Method = "POST";
            req.UserAgent = UserAgent;
            req.KeepAlive = false;

            byte[] data = Encoding.UTF8.GetBytes(content);

            req.ContentLength = data.Length;

            using (var newStream = req.GetRequestStream())
            {
                newStream.Write(data, 0, data.Length);
                newStream.Close();
            }

            var resp = req.GetResponse() as HttpWebResponse;
            if (resp != null)
            {
                if (resp.StatusCode == HttpStatusCode.OK)
                    return resp.ReadResponseString();

                resp.Close();
            }

            return null;
        }

        public static string PostBytes(string url, byte[] data, string contentType)
        {
            var req = WebRequest.Create(url) as HttpWebRequest;
            req.Method = "POST";
            req.UserAgent = UserAgent;
            req.KeepAlive = false;
            req.ContentLength = data.Length;
            if (contentType != null)
                req.ContentType = contentType;

            using (var newStream = req.GetRequestStream())
            {
                newStream.Write(data, 0, data.Length);
                newStream.Close();
            }

            var resp = req.GetResponse() as HttpWebResponse;
            if (resp != null)
            {
                if (resp.StatusCode == HttpStatusCode.OK)
                    return resp.ReadResponseString();

                resp.Close();
            }

            return null;
        }

        public static string DownloadString(string url)
        {
            var req = WebRequest.Create(url) as HttpWebRequest;
            req.UserAgent = UserAgent;
            req.KeepAlive = false;

            var resp = req.GetResponse() as HttpWebResponse;
            if (resp != null)
            {
                if (resp.StatusCode == HttpStatusCode.OK)
                    return resp.ReadResponseString();

                resp.Close();
            }

            return null;
        }

        public static ResponseStreamInfo DownloadToStream(Stream fileStream, string url, ProgressChangedEventHandler progress = null)
        {
        retry:
            try
            {
                var req = WebRequest.Create(url) as HttpWebRequest;
                req.UserAgent = UserAgent;
                req.KeepAlive = false;

                var resp = req.GetResponse() as HttpWebResponse;
                if (resp != null)
                {
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        var ret = resp.ReadResponseStream(fileStream, progress);

                        if (progress != null)
                            progress(null, new ProgressChangedEventArgs(100, null));

                        return ret;
                    }

                    resp.Close();
                }
            }
            catch (WebException ex)
            {
                if ((ex.Response as HttpWebResponse).StatusCode == (HttpStatusCode)429)
                {
                    Console.WriteLine("429 - " + url + " : Retrying");
                    System.Threading.Thread.Sleep(30000);
                    goto retry;
                }

                SimpleLogger.Instance.Error("[DownloadToStream] WebException : " + ex.Message, ex);
                throw ex;
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[DownloadToStream] Exception : " + ex.Message, ex);
                throw ex;
            }

            return null;
        }

        public static string ReadResponseString(this WebResponse response)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ReadResponseStream(response, ms);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static ResponseStreamInfo ReadResponseStream(this WebResponse response, Stream destinationStream, ProgressChangedEventHandler progress = null)
        {
            if (destinationStream == null)
                throw new ArgumentException("Stream null");

            ResponseStreamInfo ret = new ResponseStreamInfo();
            ret.ContentType = response.ContentType;

            string contentDisposition = response.Headers["Content-Disposition"];
            if (!string.IsNullOrEmpty(contentDisposition))
            {
                int idx = contentDisposition.IndexOf("filename=");
                if (idx >= 0)
                    ret.FileName = System.Uri.UnescapeDataString(contentDisposition.Substring(idx + "filename=".Length).Replace("\"", ""));
                else
                {
                    // RFC 5987 - https://greenbytes.de/tech/webdav/rfc5987.html

                    idx = contentDisposition.IndexOf("filename*=UTF-8''");
                    ret.FileName = System.Uri.UnescapeDataString(contentDisposition.Substring(idx + "filename*=UTF-8''".Length).Replace("\"", ""));
                }
            }

            if (string.IsNullOrEmpty(ret.FileName))
            {
                try { ret.FileName = System.IO.Path.GetFileName(response.ResponseUri.ToString()); }
                catch { }
            }

            if (progress != null)
                progress(null, new ProgressChangedEventArgs(0, ret.FileName));

            long length = (int)response.ContentLength;
            long pos = 0;

            using (Stream sr = response.GetResponseStream())
            {
                byte[] buffer = new byte[1024];
                int bytes = 0;

                while ((bytes = sr.Read(buffer, 0, buffer.Length)) > 0)
                {
                    destinationStream.Write(buffer, 0, bytes);

                    pos += bytes;

                    if (progress != null && length > 0)
                        progress(null, new ProgressChangedEventArgs((int)((pos * 100) / length), null));
                }

                sr.Close();
            }

            response.Close();

            if (length > 0 && pos != length)
                throw new Exception("ConnectionHelper.ReadResponseStream : Le fichier reçu est incomplet. Taille recue : " + pos + ". Taille déclarée (Content-Length) : " + length);

            return ret;
        }

        public static string DownloadFile(string url, string destinationDirectory = null, ProgressChangedEventHandler progress = null)
        {
            string fileName = Path.GetTempFileName();
            if (File.Exists(fileName))
                File.Delete(fileName);

            if (!string.IsNullOrEmpty(destinationDirectory))
                fileName = Path.Combine(destinationDirectory, Path.GetFileName(fileName));

            ResponseStreamInfo ret = null;

            try
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Create))
                    ret = DownloadToStream(fileStream, url, progress);

                if (File.Exists(fileName))
                {
                    if (ret != null && !string.IsNullOrEmpty(ret.FileName))
                    {
                        try
                        {
                            string newPath = Path.Combine(Path.GetDirectoryName(fileName), ret.FileName);

                            if (File.Exists(newPath))
                                File.Delete(newPath);

                            File.Move(fileName, newPath);
                            return newPath;
                        }
                        catch { }
                    }

                    return fileName;
                }

                throw new FileNotFoundException("File download failed");
            }
            catch
            {
                if (File.Exists(fileName))
                    File.Delete(fileName);

                throw;
            }
        }
    }

    public class ResponseStreamInfo
    {
        public string ContentType { get; internal set; }
        public string FileName { get; internal set; }
    }
}

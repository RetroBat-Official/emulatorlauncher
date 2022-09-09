using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.ComponentModel;

namespace emulatorLauncher
{
    static class WebTools
    {
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

        public static bool DownloadToStream(Stream fileStream, string url, ProgressChangedEventHandler progress = null)
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
                        resp.ReadResponseStream(fileStream, progress);

                        if (progress != null)
                            progress(null, new ProgressChangedEventArgs(100, null));

                        return true;
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

            return false;
        }

        public static string ReadResponseString(this WebResponse response)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                ReadResponseStream(response, ms);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        public static void ReadResponseStream(this WebResponse response, Stream destinationStream, ProgressChangedEventHandler progress = null)
        {
            if (destinationStream == null)
                throw new ArgumentException("Stream null");

            long length = (int)response.ContentLength;
            long pos = 0;

            try
            {
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
            }
            finally
            {
                response.Close();
            }

            if (length > 0 && pos != length)
                throw new Exception("Incomplete download : " + length);
        }
    }
}

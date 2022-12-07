using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;

namespace emulatorLauncher
{
    public class SimpleLogger
    {        
        private static SimpleLogger _instance;

        public static SimpleLogger Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SimpleLogger();

                return _instance;
            }
        }

        private const string FILE_EXT = ".log";
        private readonly string datetimeFormat;
        private readonly string logFilename;

        /// <summary>
        /// Initiate an instance of SimpleLogger class constructor.
        /// If log file does not exist, it will be created automatically.
        /// </summary>
        private SimpleLogger()
        {
            datetimeFormat = "yyyy-MM-dd HH:mm:ss.fff";
            logFilename = Path.ChangeExtension(System.Reflection.Assembly.GetEntryAssembly().Location, FILE_EXT);

            if (File.Exists(logFilename))
            {
                if (new FileInfo(logFilename).Length > 1024 * 1024)
                {
                    string prevLog = logFilename + ".old";
                    if (File.Exists(prevLog))
                        File.Delete(prevLog);

                    File.Move(logFilename, prevLog);
                }
            }
        }

        /// <summary>
        /// Log a DEBUG message
        /// </summary>
        /// <param name="text">Message</param>
        public void Debug(string text)
        {
            WriteFormattedLog(LogLevel.DEBUG, text);
        }

        /// <summary>
        /// Log an ERROR message
        /// </summary>
        /// <param name="text">Message</param>
        public void Error(string text, Exception ex = null)
        {
            WriteFormattedLog(LogLevel.ERROR, text, ex);
        }

        /// <summary>
        /// Log a FATAL ERROR message
        /// </summary>
        /// <param name="text">Message</param>
        public void Fatal(string text)
        {
            WriteFormattedLog(LogLevel.FATAL, text);
        }

        /// <summary>
        /// Log an INFO message
        /// </summary>
        /// <param name="text">Message</param>
        public void Info(string text)
        {
            WriteFormattedLog(LogLevel.INFO, text);
        }

        /// <summary>
        /// Log a TRACE message
        /// </summary>
        /// <param name="text">Message</param>
        public void Trace(string text)
        {
            WriteFormattedLog(LogLevel.TRACE, text);
        }

        /// <summary>
        /// Log a WARNING message
        /// </summary>
        /// <param name="text">Message</param>
        public void Warning(string text)
        {
            WriteFormattedLog(LogLevel.WARNING, text);
        }

        private void WriteLine(string text, bool append = true)
        {
            int err = 0;

            retry:

            try
            {
                System.Diagnostics.Debug.WriteLine(text);

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(logFilename, append, System.Text.Encoding.UTF8))
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        writer.WriteLine(text);
                    }
                }
            }
            catch (System.IO.IOException ex)
            {
                err++;
                if (err < 5)
                {
                    Thread.Sleep(5 * err);
                    goto retry;
                }

                throw ex;
            }
            catch
            {
                throw;
            }
        }

        private void WriteFormattedLog(LogLevel level, string text, Exception exception = null)
        {
            string pretext;
            switch (level)
            {
                case LogLevel.TRACE:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [TRACE]     ";
                    break;
                case LogLevel.INFO:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [INFO]      ";
                    break;
                case LogLevel.DEBUG:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [DEBUG]     ";
                    break;
                case LogLevel.WARNING:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [WARNING]   ";
                    break;
                case LogLevel.ERROR:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [ERROR]     ";
                    break;
                case LogLevel.FATAL:
                    pretext = System.DateTime.Now.ToString(datetimeFormat) + " [FATAL]     ";                                                                             
                    break;
                default:
                    pretext = "";
                    break;
            }

            WriteLine(pretext + text);

            var ex = exception;
            while (ex != null)
            {
                WriteLine(System.DateTime.Now.ToString(datetimeFormat) + " [EXCEPTION] [" + ex.GetType().Name + "] " + ex.Message);
                ex = ex.InnerException;
            }

            if (level == LogLevel.ERROR && exception != null && !string.IsNullOrEmpty(exception.StackTrace))
                WriteLine(System.DateTime.Now.ToString(datetimeFormat) + " [STACK]     [StackTrace] " + exception.StackTrace.Trim());
        }
        
        [System.Flags]
        private enum LogLevel
        {
            TRACE,
            INFO,
            DEBUG,
            WARNING,
            ERROR,
            FATAL
        }
    }
}

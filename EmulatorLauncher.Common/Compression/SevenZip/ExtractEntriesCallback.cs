using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;

namespace EmulatorLauncher.Common.Compression.SevenZip
{
    internal class ExtractEntriesCallback : IArchiveExtractCallback, ICryptoGetTextPassword, IDisposable
    {
        private ulong _bytesCount;

        public string Password { get; }
        public event ProgressChangedEventHandler Progress;

        private readonly Dictionary<uint, Entry> _entries;
        private readonly Func<Entry, string> GetOutputPath;
        private OutStreamWrapper _currentStream;

        public ExtractEntriesCallback(IEnumerable<Entry> entries, Func<Entry, string> getOutputPath, string password = null)
        {
            Password = password;
            GetOutputPath = getOutputPath;

            _entries = new Dictionary<uint, Entry>();
            foreach (var entry in entries)
                _entries[entry.Index] = entry;
        }

        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        public void SetTotal(ulong total)
        {
            _bytesCount = total;
        }

        public void SetCompleted(ref ulong completeValue)
        {
            if (Progress != null && _bytesCount != 0)
                Progress(this, new ProgressChangedEventArgs((int)(completeValue * 100 / _bytesCount), null));
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            if (_currentStream != null)
            {
                _currentStream.Dispose();
                _currentStream = null;
            }

            if (askExtractMode != AskMode.kExtract)
            {
                outStream = null;
                return 0;
            }

            if (!_entries.TryGetValue(index, out Entry entry))
            {           
                outStream = null;
                return 0;
            }

            string outputPath = GetOutputPath(entry);

            try
            {
                if (outputPath == null)
                {              
                    outStream = null;
                    return 0;
                }

                if (entry.IsFolder)
                {
                    Directory.CreateDirectory(outputPath);
                    outStream = null;
                    return 0;
                }

                string directoryName = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrWhiteSpace(directoryName))
                    Directory.CreateDirectory(directoryName);

                if (File.Exists(outputPath))
                    File.Delete(outputPath);

                var stream = File.Create(outputPath);
                if (stream != null)
                {
                    outStream = new OutStreamWrapper(stream);
                    _currentStream = outStream as OutStreamWrapper;
                    return 0;
                }
            }
            catch { }

            outStream = null;
            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode) { }
        public void SetOperationResult(OperationResult resultEOperationResult) { }

        public void Dispose()
        {
            if (_currentStream != null)
            {
                _currentStream.Dispose();
                _currentStream = null;
            }
        }
    }
}
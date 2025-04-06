using System.ComponentModel;
using System.IO;

namespace EmulatorLauncher.Common.Compression.SevenZip
{
    internal class ArchiveStreamCallback : IArchiveExtractCallback, ICryptoGetTextPassword
    {
        private readonly uint fileNumber;
        private readonly Stream stream;
        private ulong _bytesCount;

        public string Password { get; }

        public event ProgressChangedEventHandler Progress;

        public ArchiveStreamCallback(uint fileNumber, Stream stream, string password = null)
        {
            this.fileNumber = fileNumber;
            this.stream = stream;
            Password = password ?? "";
        }

        public void SetTotal(ulong total)
        {
            _bytesCount = total;
        }

        public void SetCompleted(ref ulong completeValue)
        {
            if (Progress != null && _bytesCount != 0)
                Progress(this, new ProgressChangedEventArgs((int) (completeValue * 100 / _bytesCount), null));
        }

        public int CryptoGetTextPassword(out string password)
        {
            password = this.Password;
            return 0;
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            if ((index != this.fileNumber) || (askExtractMode != AskMode.kExtract))
            {
                outStream = null;
                return 0;
            }

            outStream = new OutStreamWrapper(this.stream);

            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
        }
    }
}
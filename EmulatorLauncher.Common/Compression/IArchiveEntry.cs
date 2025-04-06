using System;

namespace EmulatorLauncher.Common.Compression
{
    public interface IArchiveEntry
    {       
        string Filename { get; }
        bool IsDirectory { get; }
        DateTime LastModified { get; }
        long Length { get; }

        uint Crc32 { get; }
        string HexCrc { get; }

        void Extract(string directory);
    }
}
using EmulatorLauncher.Common;
using Microsoft.Win32;
using Newtonsoft.Json;
using Steam_Library_Manager.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ValveKeyValue;

namespace EmulatorLauncher
{
    class SteamAppInfoReader
    {
        public SteamAppInfoReader()
        {
            Apps = new List<SteamAppInfo>();
        }

        private const uint Magic28 = 0x07564428;
        private const uint Magic29 = 0x07564429;
        private const uint Magic = 0x07564427;

        public List<SteamAppInfo> Apps { get; private set; }

        /// <summary>
        /// Opens and reads the given filename.
        /// </summary>
        /// <param name="filename">The file to open and read.</param>
        public void Read(string filename)
        {
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Read(fs);
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        public void Read(Stream input)
        {
            using (var reader = new BinaryReader(input))
            {
                var magic = reader.ReadUInt32();

                if (magic != Magic && magic != Magic28 && magic != Magic29)
                {
                    throw new InvalidDataException("Unknown magic header");
                }

                uint universe = reader.ReadUInt32();

                var options = new KVSerializerOptions();

                if (magic == Magic29)
                {
                    var stringTableOffset = reader.ReadInt64();
                    var offset = reader.BaseStream.Position;
                    reader.BaseStream.Position = stringTableOffset;
                    var stringCount = reader.ReadUInt32();
                    var stringPool = new string[stringCount];

                    for (var i = 0; i < stringCount; i++)
                    {
                        stringPool[i] = ReadNullTermUtf8String(reader.BaseStream);
                    }

                    reader.BaseStream.Position = offset;
                }

                var deserializer = ValveKeyValue.KVSerializer.Create(ValveKeyValue.KVSerializationFormat.KeyValues1Binary);

                do
                {
                    var appid = reader.ReadUInt32();
                    if (appid == 0)
                        break;

                    uint size = reader.ReadUInt32(); // size until end of Data
                    var end = reader.BaseStream.Position + size;

                    var app = new SteamAppInfo
                    {
                        AppID = appid,
                        InfoState = reader.ReadUInt32(),
                        LastUpdated = DateTimeFromUnixTime(reader.ReadUInt32()),
                        Token = reader.ReadUInt64(),
                        Hash = reader.ReadBytes(20),
                        ChangeNumber = reader.ReadUInt32(),
                    };

                    uint remaining = size - 4 - 4 - 8 - 20 - 4;

                    if (magic == Magic28 || magic == Magic29)
                    {
                        app.BinaryDataHash = reader.ReadBytes(20);
                        remaining = remaining - 20;
                    }

                    app.Data = deserializer.Deserialize(input);

                    Apps.Add(app);
                } while (true);
            }
        }

        public static DateTime DateTimeFromUnixTime(uint unixTime)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTime);
        }

        private static string ReadNullTermUtf8String(Stream stream)
        {
            byte[] buffer = new byte[32];
            int position = 0;

            while (true)
            {
                int b = stream.ReadByte();
                if (b <= 0) // null byte or stream ended
                {
                    break;
                }

                if (position >= buffer.Length)
                {
                    // Double the buffer size
                    Array.Resize(ref buffer, buffer.Length * 2);
                }

                buffer[position++] = (byte)b;
            }

            return Encoding.UTF8.GetString(buffer, 0, position);
        }
    }

    class SteamAppInfo
    {
        public uint AppID { get; set; }
        public uint InfoState { get; set; }
        public DateTime LastUpdated { get; set; }
        public ulong Token { get; set; }
        public byte[] Hash { get; set; }
        public byte[] BinaryDataHash { get; set; }
        public uint ChangeNumber { get; set; }
        public ValveKeyValue.KVObject Data { get; set; }
    }
        
}

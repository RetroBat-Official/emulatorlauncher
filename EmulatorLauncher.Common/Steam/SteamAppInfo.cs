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
        /// <param name="appIdFilter">Only read this app (0 = read all apps).</param>
        public void Read(string filename, uint appIdFilter = 0)
        {
            using (var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Read(fs, appIdFilter);
        }

        /// <summary>
        /// Reads the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="input">The input <see cref="Stream"/> to read from.</param>
        /// <param name="appIdFilter">Only read this app (0 = read all apps).</param>
        public void Read(Stream input, uint appIdFilter = 0)
        {
            using (var reader = new BinaryReader(input))
            {
                // Magic is 0x075644XX, XX being the file version (39 = 0x27, 40 = 0x28, 41 = 0x29)
                var magic = reader.ReadUInt32();
                uint version = magic & 0xFF;

                if ((magic >> 8) != 0x075644 || version < 39)
                    throw new InvalidDataException("Unknown magic header: 0x" + magic.ToString("X8"));

                uint universe = reader.ReadUInt32();

                var options = new KVSerializerOptions();

                // Version 41+ (June 2024) : key names are stored once, in a string table at the end of the file
                if (version >= 41)
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

                    options.StringTable = new StringTable(stringPool);
                }

                var deserializer = ValveKeyValue.KVSerializer.Create(ValveKeyValue.KVSerializationFormat.KeyValues1Binary);

                do
                {
                    var appid = reader.ReadUInt32();
                    if (appid == 0)
                        break;

                    uint size = reader.ReadUInt32(); // size until end of Data
                    var end = reader.BaseStream.Position + size;

                    // Skip the apps we are not looking for
                    if (appIdFilter != 0 && appid != appIdFilter)
                    {
                        reader.BaseStream.Position = end;
                        continue;
                    }

                    var app = new SteamAppInfo
                    {
                        AppID = appid,
                        InfoState = reader.ReadUInt32(),
                        LastUpdated = DateTimeFromUnixTime(reader.ReadUInt32()),
                        Token = reader.ReadUInt64(),
                        Hash = reader.ReadBytes(20),
                        ChangeNumber = reader.ReadUInt32(),
                    };

                    if (version >= 40)
                        app.BinaryDataHash = reader.ReadBytes(20);

                    app.Data = deserializer.Deserialize(input, options);

                    if (reader.BaseStream.Position != end)
                        throw new InvalidDataException("Unexpected appinfo.vdf structure");

                    Apps.Add(app);

                    if (appIdFilter != 0)
                        break;
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

using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using EmulatorLauncher.Common;

namespace EmulatorLauncher
{
    /// <summary>
    /// ArcadeDuck save states use the DuckStation container (save_state_version.h, version 71) :
    /// the screenshot is RGBA8, compressed since version 69 with the save state compression mode.
    /// The generator forces Deflate (zlib stream) so that the screenshot can be extracted here.
    /// </summary>
    class ArcadeDuckSaveStatesMonitor : SaveStatesWatcher
    {
        private const uint SAVE_STATE_MAGIC = 0x43435544;
        private const uint ARCADE_SAVE_STATE_MAGIC = 0x53414441;

        private const uint COMPRESSION_NONE = 0;
        private const uint COMPRESSION_DEFLATE = 1;

        public ArcadeDuckSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath)
        {
        }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                var bytes = File.ReadAllBytes(saveState);
                if (bytes.Length < Marshal.SizeOf(typeof(SAVE_STATE_HEADER)))
                    return;

                SAVE_STATE_HEADER header;
                GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try { header = (SAVE_STATE_HEADER)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SAVE_STATE_HEADER)); }
                finally { handle.Free(); }

                if (header.magic != SAVE_STATE_MAGIC && header.magic != ARCADE_SAVE_STATE_MAGIC)
                    return;

                int width = (int)header.screenshot_width;
                int height = (int)header.screenshot_height;
                int pixelsSize = width * height * 4;

                if (width <= 0 || height <= 0 || header.screenshot_compressed_size == 0 || (long)header.offset_to_screenshot + header.screenshot_compressed_size > bytes.Length)
                    return;

                // Before version 69, the screenshot was always uncompressed
                uint compression = header.version >= 69 ? header.screenshot_compression_type : COMPRESSION_NONE;

                byte[] pixels;
                if (compression == COMPRESSION_NONE)
                {
                    if (header.screenshot_compressed_size < pixelsSize)
                        return;

                    pixels = new byte[pixelsSize];
                    Buffer.BlockCopy(bytes, (int)header.offset_to_screenshot, pixels, 0, pixelsSize);
                }
                else if (compression == COMPRESSION_DEFLATE)
                {
                    // zlib stream (compress2) : skip the 2 bytes zlib header, DeflateStream reads raw deflate data
                    pixels = new byte[pixelsSize];
                    using (var ms = new MemoryStream(bytes, (int)header.offset_to_screenshot + 2, (int)header.screenshot_compressed_size - 2))
                    using (var deflate = new DeflateStream(ms, CompressionMode.Decompress))
                    {
                        int read = 0;
                        while (read < pixelsSize)
                        {
                            int count = deflate.Read(pixels, read, pixelsSize - read);
                            if (count <= 0)
                                break;

                            read += count;
                        }

                        if (read != pixelsSize)
                            return;
                    }
                }
                else
                    return; // Zstandard : not supported by .NET Framework

                using (var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    var data = bmp.LockBits(new System.Drawing.Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    // RGBA (ArcadeDuck) to BGRA (GDI+), alpha forced to opaque
                    byte[] row = new byte[width * 4];
                    for (int y = 0; y < height; y++)
                    {
                        int src = y * width * 4;
                        for (int x = 0; x < width; x++)
                        {
                            int i = x * 4;
                            row[i] = pixels[src + i + 2];
                            row[i + 1] = pixels[src + i + 1];
                            row[i + 2] = pixels[src + i];
                            row[i + 3] = 0xFF;
                        }

                        Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                    }

                    bmp.UnlockBits(data);
                    bmp.Save(destScreenShot, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        struct SAVE_STATE_HEADER
        {
            public UInt32 magic;
            public UInt32 version;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] title;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] serial;

            public UInt32 media_path_length;
            public UInt32 offset_to_media_path;
            public UInt32 media_subimage_index;

            public UInt32 screenshot_compression_type;
            public UInt32 screenshot_width;
            public UInt32 screenshot_height;
            public UInt32 screenshot_compressed_size;
            public UInt32 offset_to_screenshot;

            public UInt32 data_compression_type;
            public UInt32 data_compressed_size;
            public UInt32 data_uncompressed_size;
            public UInt32 offset_to_data;
        };
    }
}

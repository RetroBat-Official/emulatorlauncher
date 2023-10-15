using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EmulatorLauncher.Common;
using System.IO;
using System.Runtime.InteropServices;

namespace EmulatorLauncher
{
    class DuckStationSaveStatesMonitor : SaveStatesWatcher
    {
        public DuckStationSaveStatesMonitor(string romfile, string emulatorPath, string sharedPath)
            : base(romfile, emulatorPath, sharedPath)
        {
        }

        protected override void SaveScreenshot(string saveState, string destScreenShot)
        {
            try
            {
                var bytes = File.ReadAllBytes(saveState);

                GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                SAVE_STATE_HEADER header = (SAVE_STATE_HEADER)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SAVE_STATE_HEADER));
                handle.Free();

                if (header.magic != 0x43435544) // SAVE_STATE_MAGIC
                    return;

                if (header.screenshot_size == 0 || header.screenshot_size != header.screenshot_width * header.screenshot_height * 4)
                    return;

                byte[] imageBytes = bytes.Skip((int)header.offset_to_screenshot).Take((int)header.screenshot_size).ToArray();

                using (var bmp = new System.Drawing.Bitmap(header.screenshot_width, header.screenshot_height))
                {
                    var dstData = bmp.LockBits(new System.Drawing.Rectangle(0, 0, bmp.Size.Width, bmp.Size.Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                    for (int i = 0; i < header.screenshot_size; i++)
                        Marshal.WriteByte(dstData.Scan0, i, imageBytes[i]);

                    bmp.UnlockBits(dstData);

                    bmp.Save(destScreenShot);
                }
            }
            catch { }
        }

        struct SAVE_STATE_HEADER
        {
            public UInt32 magic;
            public UInt32 version;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] title;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] serial;

            public UInt32 media_filename_length;
            public UInt32 offset_to_media_filename;
            public UInt32 media_subimage_index;
            public UInt32 unused_offset_to_playlist_filename; // Unused as of version 51.

            public Int32 screenshot_width;
            public Int32 screenshot_height;
            public Int32 screenshot_size;
            public Int32 offset_to_screenshot;

            public UInt32 data_compression_type;
            public UInt32 data_compressed_size;
            public UInt32 data_uncompressed_size;
            public UInt32 offset_to_data;
        };
    }
}

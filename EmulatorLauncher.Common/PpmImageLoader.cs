using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace EmulatorLauncher.Common
{
    public class PpmImageLoader
    {
        public static Bitmap LoadPpm(string filePath)
        {
            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                using (var br = new BinaryReader(fs))
                {
                    string magic = ReadToken(br);
                    if (magic != "P6")
                        throw new NotSupportedException("Only PPM P6 supported");

                    int width = int.Parse(ReadToken(br));
                    int height = int.Parse(ReadToken(br));
                    int maxVal = int.Parse(ReadToken(br));

                    if (maxVal != 255)
                        throw new NotSupportedException("Only maxVal=255 supported");

                    var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    var rect = new Rectangle(0, 0, width, height);
                    var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

                    try
                    {
                        int stride = data.Stride;
                        int bytesPerPixel = 3;

                        byte[] row = new byte[width * bytesPerPixel];

                        for (int y = 0; y < height; y++)
                        {
                            // Read one scanline from PPM (RGBRGB...)
                            ReadFully(br, row, 0, row.Length);

                            // Destination pointer
                            IntPtr dst = data.Scan0 + y * stride;

                            // Convert RGB → BGR
                            for (int x = 0; x < width; x++)
                            {
                                int src = x * 3;
                                int dstIdx = x * 3;

                                Marshal.WriteByte(dst, dstIdx + 0, row[src + 2]); // B
                                Marshal.WriteByte(dst, dstIdx + 1, row[src + 1]); // G
                                Marshal.WriteByte(dst, dstIdx + 2, row[src + 0]); // R
                            }
                        }
                    }
                    finally
                    {
                        bmp.UnlockBits(data);
                    }

                    return bmp;
                }
            }
        }

        private static string ReadToken(BinaryReader br)
        {
            var sb = new StringBuilder();
            byte b;

            // skip whitespace
            do
            {
                b = br.ReadByte();
            }
            while (char.IsWhiteSpace((char)b));

            // skip comments
            if (b == '#')
            {
                while (br.ReadByte() != '\n') ;
                return ReadToken(br);
            }

            sb.Append((char)b);
            while (!char.IsWhiteSpace((char)(b = br.ReadByte())))
                sb.Append((char)b);

            return sb.ToString();
        }

        private static void ReadFully(BinaryReader br, byte[] buffer, int offset, int count)
        {
            int read;
            while (count > 0 &&
                   (read = br.Read(buffer, offset, count)) > 0)
            {
                offset += read;
                count -= read;
            }

            if (count != 0)
                throw new EndOfStreamException();
        }
    }
}

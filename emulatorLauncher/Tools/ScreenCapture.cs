using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;

namespace emulatorLauncher.Tools
{
    class ScreenCapture
    {
        public static void AddImageToGameList(string rom, string imagePath, bool resizeTo43 = true)
        {
            if (string.IsNullOrEmpty(rom))
                return;

            try
            {
                string gameListPath = System.IO.Path.Combine(Path.GetDirectoryName(rom), "gamelist.xml");
                if (!File.Exists(gameListPath))
                    return;

                GameList gameList = GameList.Load(gameListPath);
                if (gameList == null)
                    return;

                string romName = Path.GetFileName(rom);
                var game = gameList.Games.FirstOrDefault(g => Path.GetFileName(g.path).Equals(romName, StringComparison.InvariantCultureIgnoreCase));
                if (game != null && game.ImageExists())
                    return;

                string fullImagePath = Path.Combine(Path.GetDirectoryName(rom), "images", Path.GetFileNameWithoutExtension(romName) + ".jpg");
                if (File.Exists(fullImagePath))
                    return;

                if (game == null)
                {
                    game = new Game();
                    game.Name = Path.GetFileNameWithoutExtension(romName);
                    game.path = ".\\" + Path.GetFileName(romName);
                    gameList.Games.Add(game);
                }

                string partialImagePath = ".\\" + Path.Combine("images", Path.GetFileNameWithoutExtension(romName) + ".jpg");
                using (Image img = (imagePath == null ? CaptureScreen() : Image.FromFile(imagePath)))
                {
                    if (!resizeTo43)
                    {
                        var codecInfo = GetEncoderInfo("image/jpeg");
                        if (codecInfo == null)
                            return;

                        var encoderParameters = new EncoderParameters(1);
                        encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);
                        img.Save(fullImagePath, codecInfo, encoderParameters);
                    }
                    else
                    {
                        using (Bitmap bmp = new Bitmap(1280, 1024))
                        {
                            using (Graphics g = Graphics.FromImage(bmp))
                            {
                                Rectangle rect = Misc.GetPictureRect(img.Size, new Rectangle(0, 0, bmp.Width, bmp.Height), true);
                                g.DrawImage(img, rect);
                            }

                            var codecInfo = GetEncoderInfo("image/jpeg");
                            if (codecInfo == null)
                                return;

                            var encoderParameters = new EncoderParameters(1);
                            encoderParameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 75L);

                            bmp.Save(fullImagePath, codecInfo, encoderParameters);
                        }
                    }
                }

                game.Image = partialImagePath;
                gameList.Save();
            }
            catch { }
        }

        public static void AddScreenCaptureToGameList(string rom)
        {
            AddImageToGameList(rom, null);
        }

        static ImageCodecInfo GetEncoderInfo(String mimeType)
        {
            int j;
            ImageCodecInfo[] encoders;
            encoders = ImageCodecInfo.GetImageEncoders();
            for (j = 0; j < encoders.Length; ++j)
            {
                if (encoders[j].MimeType == mimeType)
                    return encoders[j];
            }
            return null;
        }
        
        static Bitmap CaptureScreen()
        {
            var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb);

            using (var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

            return bmpScreenshot;
        }

    }
}

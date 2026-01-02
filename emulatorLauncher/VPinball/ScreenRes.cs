using System.Text;
using System.IO;
using Microsoft.Win32;

namespace EmulatorLauncher.VPinballLauncher
{
    class ScreenRes
    {        
        public object Monitor { get; set; }
        public object DmdFlipY { get; set; }
        public object DmdPosX { get; set; }
        public object DmdPosY { get; set; }
        public object DmdResX { get; set; }
        public object DmdResY { get; set; }

        public object Screen2posX { get; set; }
        public object Screen2posY { get; set; }
        public object Screen2ResX { get; set; }
        public object Screen2ResY { get; set; }
        public object ScreenResX { get; set; }
        public object ScreenResY { get; set; }
        public object Screen2posXStart { get; set; }
        public object Screen2posYStart { get; set; }
        public object Screen2ResXStart { get; set; }
        public object Screen2ResYStart { get; set; }
        public object FramePath { get; set; }

        private string _fileName;

        public static ScreenRes Load(string romPath)
        {
            string fn = Path.Combine(romPath, "ScreenRes.txt");           
            if (!File.Exists(fn))
                return new ScreenRes() { _fileName = fn };

            ScreenRes ret = new ScreenRes() { _fileName = fn };            

            string[] lines = File.ReadAllLines(fn);
            if (lines.Length >= 12)
            {
                ret.ScreenResX = lines[0];
                ret.ScreenResY = lines[1];
                ret.Screen2ResX = lines[2];
                ret.Screen2ResY = lines[3];
                ret.Monitor = lines[4];
                ret.Screen2posX = lines[5];
                ret.Screen2posY = lines[6];
                ret.DmdResX = lines[7];
                ret.DmdResY = lines[8];
                ret.DmdPosX = lines[9];
                ret.DmdPosY = lines[10];
                ret.DmdFlipY = lines[11];
                ret.Screen2posXStart = lines[12];
                ret.Screen2posYStart = lines[13];
                ret.Screen2ResXStart = lines[14];
                ret.Screen2ResYStart = lines[15];
                ret.FramePath = lines[16];
            }

            return ret;
        }

        public void Save()
        {
            if (string.IsNullOrEmpty(_fileName))
                return;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine(ScreenResX == null ? "0" : ScreenResX.ToString());
            sb.AppendLine(ScreenResY == null ? "0" : ScreenResY.ToString());
            sb.AppendLine(Screen2ResX == null ? "0" : Screen2ResX.ToString());
            sb.AppendLine(Screen2ResY == null ? "0" : Screen2ResY.ToString());
            sb.AppendLine(Monitor == null ? "1" : Monitor.ToString());
            sb.AppendLine(Screen2posX == null ? "0" : Screen2posX.ToString());
            sb.AppendLine(Screen2posY == null ? "0" : Screen2posY.ToString());
            sb.AppendLine(DmdResX == null ? "0" : DmdResX.ToString());
            sb.AppendLine(DmdResY == null ? "0" : DmdResY.ToString());
            sb.AppendLine(DmdPosX == null ? "0" : DmdPosX.ToString());
            sb.AppendLine(DmdPosY == null ? "0" : DmdPosY.ToString());
            sb.AppendLine(DmdFlipY == null ? "0" : DmdFlipY.ToString());
            sb.AppendLine(Screen2posXStart == null ? "0" : Screen2posXStart.ToString());
            sb.AppendLine(Screen2posYStart == null ? "0" : Screen2posYStart.ToString());
            sb.AppendLine(Screen2ResXStart == null ? "0" : Screen2ResXStart.ToString());
            sb.AppendLine(Screen2ResYStart == null ? "0" : Screen2ResYStart.ToString());
            sb.AppendLine(FramePath == null ? "0" : FramePath.ToString());

            File.WriteAllText(_fileName, sb.ToString());

            ForceDisableB2S(false);
        }

        private void ForceDisableB2S(bool value)
        {            
            RegistryKey regKeyc = Registry.CurrentUser.OpenSubKey(@"Software", true);
            if (regKeyc != null)
                regKeyc = regKeyc.CreateSubKey("Visual Pinball").CreateSubKey("Controller");

            if (regKeyc != null)
            {
                regKeyc.SetValue("ForceDisableB2S", value ? 1 : 0);
                regKeyc.Close();
            }
        }

        public void Delete()
        {
            try
            {
                ForceDisableB2S(true);

                if (File.Exists(_fileName))
                    File.Delete(_fileName);
            }
            catch { }
        }

 

    }
}

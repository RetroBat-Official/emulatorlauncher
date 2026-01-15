using Microsoft.Win32;
using System;
using System.IO;
using System.Text;

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
            
            var setters = new Action<string>[]
            {
                v => ret.ScreenResX = v,
                v => ret.ScreenResY = v,
                v => ret.Screen2ResX = v,
                v => ret.Screen2ResY = v,
                v => ret.Monitor = v,
                v => ret.Screen2posX = v,
                v => ret.Screen2posY = v,
                v => ret.DmdResX = v,
                v => ret.DmdResY = v,
                v => ret.DmdPosX = v,
                v => ret.DmdPosY = v,
                v => ret.DmdFlipY = v,
                v => ret.Screen2posXStart = v,
                v => ret.Screen2posYStart = v,
                v => ret.Screen2ResXStart = v,
                v => ret.Screen2ResYStart = v,
                v => ret.FramePath = v,
            };

            for (int i = 0; i < lines.Length && i < setters.Length; i++)
            {
                setters[i](lines[i]);
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

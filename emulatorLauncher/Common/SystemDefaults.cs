using System.Linq;
using System.Text;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher
{
    class SystemDefaults
    {
        class YmlSystem
        {
            [YmlName]
            public string system { get; set; }

            public string emulator { get; set; }
            public string core { get; set; }

            public YmlOptions options { get; set; }

            public override string ToString()
            {
                return system;
            }
        }

        class YmlOptions
        {
            public string videomode { get; set; }
            public string ratio { get; set; }
            public string video_threaded { get; set; }
            public string smooth { get; set; }
            public string rewind { get; set; }
            public string bezel { get; set; }
            public string forceNoBezel { get; set; }
        }

        public static string GetDefaultEmulator(string system)
        {
            EnsureCache();
            return _ymlSystemsCache.Where(i => i.system == system).Select(i => i.emulator).FirstOrDefault();
        }

        public static string GetDefaultCore(string system)
        {
            EnsureCache();
            return _ymlSystemsCache.Where(i => i.system == system).Select(i => i.core).FirstOrDefault();
        }

        private static void EnsureCache()
        {
            if (_ymlSystemsCache == null)
                _ymlSystemsCache = SimpleYml<YmlSystem>.Parse(Encoding.UTF8.GetString(Properties.Resources.configgen_defaults));
        }

        private static SimpleYml<YmlSystem> _ymlSystemsCache;
    }



}

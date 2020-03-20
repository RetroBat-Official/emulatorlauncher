using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{  
    class SystemShaders
    {
        class YmlShader : IYmlItem
        {
            public string system { get; set; }

            public string shader { get; set; }
            public string scanline { get; set; }
        }

        public static string GetShader(string yml, string system)
        {
            if (_ymlShadersCache == null)
                _ymlShadersCache = new SimpleYml<YmlShader>(yml);

            return _ymlShadersCache.Where(i => i.system == system).Select(i => i.shader).FirstOrDefault();
        }

        private static SimpleYml<YmlShader> _ymlShadersCache;
    }
}

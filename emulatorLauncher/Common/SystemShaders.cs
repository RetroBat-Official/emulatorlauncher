using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using emulatorLauncher.Tools;

namespace emulatorLauncher
{  
    class SystemShaders
    {
        class YmlShader
        {
            [YmlName]
            public string system { get; set; }

            public string shader { get; set; }
            public string scanline { get; set; }
        }

        public static string GetShader(string yml, string system, string emulator, string core)
        {
            if (_ymlShadersCache == null)
                _ymlShadersCache = YmlFile.Parse(yml);

            retryWithDefault:

            var container = _ymlShadersCache.GetContainer(system);
            if (container != null)
            {
                bool found = false;

				foreach (var si in container.OfType<YmlElement>())
				{								
					if (emulator == "libretro" && si.Name == "shader")
						found = true;
					else if (!string.IsNullOrEmpty(emulator) && !string.IsNullOrEmpty(core) && si.Name == emulator + "." + core)
						found = true;
					else if (!string.IsNullOrEmpty(emulator) && si.Name == emulator)
						found = true;

					if (found)
					{
						if (si.Value != null && si.Value.Contains("disabled"))
							found = false;

                        return si.Value;
					}
				}				
            }

            if (system != "default")
            {
                system = "default";
                goto retryWithDefault;
            }

            return GetDefaultShader();
        }

        private static string GetDefaultShader()
        {
            var def = _ymlShadersCache.GetContainer("default");
            if (def != null)
                return def["shader"];

            return "";
        }

        private static YmlFile _ymlShadersCache;
    }
}

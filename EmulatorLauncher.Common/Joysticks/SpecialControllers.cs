using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;
using Newtonsoft.Json;

namespace EmulatorLauncher.Common.Joysticks
{
    #region n64
    public class N64Controller
    {
        static readonly N64Controller[] N64Controllers;

        public string Emulator { get; private set; }
        public string Name { get; private set; }
        public string Guid { get; private set; }
        public string Driver { get; private set; }
        public Dictionary<string,string> Mapping { get; private set; }
        public Dictionary<string, string> HotKeyMapping { get; private set; }
        public Dictionary<string, string> ControllerInfo { get; private set; }

        #region Private methods
        private N64Controller(string emulator, string name, string guid, string driver, Dictionary<string, string> mapping, Dictionary<string, string> hotkeymapping = null, Dictionary<string, string> controllerInfo = null)
        {
            Emulator = emulator;
            Name = name;
            Guid = guid;
            Driver = driver;
            Mapping = mapping;
            HotKeyMapping = hotkeymapping;
            ControllerInfo = controllerInfo;
        }
        #endregion

        #region public methods
        public static N64Controller GetN64Controller(string emulator, string guid, List<N64Controller> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null)
                return null;

            return controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                c.Guid.Split(',')
                    .Select(g => g.Trim())
                    .Any(g => string.Equals(g, guid, StringComparison.InvariantCultureIgnoreCase))
            );
        }

        public static N64Controller GetN64Controller(string emulator, string guid, string driver, List<N64Controller> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null)
                return null;

            var ret = controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(c.Driver, driver, StringComparison.InvariantCultureIgnoreCase) &&
                c.Guid.Split(',')
                    .Select(g => g.Trim())
                    .Any(g => string.Equals(g, guid, StringComparison.InvariantCultureIgnoreCase))
                );
            if (ret == null)
                ret = controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                c.Guid.Split(',')
                    .Select(g => g.Trim())
                    .Any(g => string.Equals(g, guid, StringComparison.InvariantCultureIgnoreCase))
                );

            return ret == null ? null : ret;
        }

        public static List<N64Controller> LoadControllersFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"The JSON file '{jsonFilePath}' was not found.");

            try
            {
                var jsonContent = File.ReadAllText(jsonFilePath);
                var rootObject = JsonConvert.DeserializeObject<RootObject>(jsonContent);
                var controllers = new List<N64Controller>();
                
                foreach (var item in rootObject.Controllers)
                {
                    var mappingDict = item.Mapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    controllers.Add(new N64Controller(
                        item.Emulator,
                        item.Name,
                        item.Guid,
                        item.Driver,
                        mappingDict,
                        item.HotKeyMapping ?? new Dictionary<string, string>(),
                        item.ControllerInfo ?? new Dictionary<string, string>()
                    ));
                }

                return controllers;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load or parse the JSON file.", ex);
            }
        }
        #endregion
    }
    #endregion

    #region megadrive
    public class MegadriveController
    {
        static readonly MegadriveController[] MegadriveControllers;
        public string Emulator { get; private set; }
        public string Name { get; private set; }
        public string Guid { get; private set; }
        public string Driver { get; private set; }
        public Dictionary<string, string> Mapping { get; private set; }
        public Dictionary<string, string> HotKeyMapping { get; private set; }
        public Dictionary<string, string> ControllerInfo { get; private set; }

        #region Private methods
        private MegadriveController(string emulator, string name, string guid, string driver, Dictionary<string, string> mapping, Dictionary<string, string> hotkeymapping = null, Dictionary<string, string> controllerInfo = null)
        {
            Emulator = emulator;
            Name = name;
            Guid = guid;
            Driver = driver;
            Mapping = mapping;
            HotKeyMapping = hotkeymapping;
            ControllerInfo = controllerInfo;
        }
        #endregion

        #region public methods
        public static MegadriveController GetMDController(string emulator, string guid, string driver, List<MegadriveController> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null || driver == null)
                return null;

            var ret = controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(c.Driver, driver, StringComparison.InvariantCultureIgnoreCase) &&
                c.Guid.Split(',')
                    .Select(g => g.Trim())
                    .Any(g => string.Equals(g, guid, StringComparison.InvariantCultureIgnoreCase))
                );
            if (ret == null)
                ret = controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                c.Guid.Split(',')
                    .Select(g => g.Trim())
                    .Any(g => string.Equals(g, guid, StringComparison.InvariantCultureIgnoreCase))
                );

            return ret == null ? null : ret;
        }

        public static MegadriveController GetMDController(string emulator, string guid, List<MegadriveController> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null)
                return null;

            var ret = controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(c.Guid, guid, StringComparison.InvariantCultureIgnoreCase));
            return ret == null ? null : ret;
        }

        public static List<MegadriveController> LoadControllersFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"The JSON file '{jsonFilePath}' was not found.");
            
            try
            {
                var jsonContent = File.ReadAllText(jsonFilePath);
                var rootObject = JsonConvert.DeserializeObject<RootObject>(jsonContent);
                var controllers = new List<MegadriveController>();

                foreach (var item in rootObject.Controllers)
                {
                    var mappingDict = item.Mapping.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    controllers.Add(new MegadriveController(
                        item.Emulator,
                        item.Name,
                        item.Guid,
                        item.Driver,
                        mappingDict,
                        item.HotKeyMapping ?? new Dictionary<string, string>(),
                        item.ControllerInfo ?? new Dictionary<string, string>()
                    ));
                }

                return controllers;
            }
            
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to load or parse the JSON file.", ex);
            }
        }
        #endregion
    }
    #endregion

    public class RootObject
    {
        public List<ControllerItem> Controllers { get; set; }
    }

    // This class represents each controller item in the JSON array
    public class ControllerItem
    {
        public string Emulator { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }
        public string Driver { get; set; }
        public Dictionary<string, string> Mapping { get; set; }
        public Dictionary<string, string> HotKeyMapping { get; set; }
        public Dictionary<string, string> ControllerInfo { get; set; }
    }
}
using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.Joysticks
{
    public class N64Controller
    {
        static readonly N64Controller[] N64Controllers;

        public string Emulator { get; private set; }
        public string Name { get; private set; }
        public string Guid { get; private set; }
        public Dictionary<string,string> Mapping { get; private set; }
        public Dictionary<string, string> HotKeyMapping { get; private set; }
        public Dictionary<string, string> ControllerInfo { get; private set; }

        #region Private methods
        private N64Controller(string emulator, string name, string guid, Dictionary<string, string> mapping, Dictionary<string, string> hotkeymapping = null, Dictionary<string, string> controllerInfo = null)
        {
            Emulator = emulator;
            Name = name;
            Guid = guid;
            Mapping = mapping;
            HotKeyMapping = hotkeymapping;
            ControllerInfo = controllerInfo;
        }

        private static Dictionary<string, string> ConvertDynamicJsonToDictionary(DynamicJson dynamicJson)
        {
            var dictionary = new Dictionary<string, string>();

            foreach (var key in dynamicJson.GetDynamicMemberNames())
            {
                var value = dynamicJson[key];
                dictionary[key] = value?.ToString();
            }

            return dictionary;
        }
        #endregion

        #region public methods
        public static N64Controller GetN64Controller(string emulator, string guid)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid))
                return null;

            return N64Controllers.FirstOrDefault(c =>
                emulator.Equals(c.Emulator, StringComparison.InvariantCultureIgnoreCase) &&
                guid.Equals(c.Guid, StringComparison.InvariantCultureIgnoreCase));
        }

        public static N64Controller GetN64Controller(string emulator, string guid, List<N64Controller> controllers)
        {
            if (string.IsNullOrEmpty(emulator) || string.IsNullOrEmpty(guid) || controllers == null)
                return null;

            return controllers.FirstOrDefault(c =>
                string.Equals(c.Emulator, emulator, StringComparison.InvariantCultureIgnoreCase) &&
                string.Equals(c.Guid, guid, StringComparison.InvariantCultureIgnoreCase));
        }

        public static List<N64Controller> LoadControllersFromJson(string jsonFilePath)
        {
            if (!File.Exists(jsonFilePath))
                throw new FileNotFoundException($"The JSON file '{jsonFilePath}' was not found.");

            try
            {
                var dynamicJson = DynamicJson.Load(jsonFilePath);

                var controllers = new List<N64Controller>();

                // Assuming 'Controllers' is the key for your array
                var controllerArray = dynamicJson.GetArray("Controllers");
                foreach (var item in controllerArray.Cast<DynamicJson>())
                {
                    var emulator = item["Emulator"];
                    var name = item["Name"];
                    var guid = item["Guid"];

                    // Convert mappings
                    var mapping = item.GetObject("Mapping");
                    var mappingDict = ConvertDynamicJsonToDictionary(mapping);
                    mappingDict = mappingDict
                    .ToDictionary(
                        kvp => kvp.Key.Replace("__", " "),
                        kvp => kvp.Value.Replace("__", " ")
                    );

                    // Convert HotKeyMapping
                    var hotKeyMapping = item.GetObject("HotKeyMapping");
                    var hotKeyMappingDict = hotKeyMapping != null ? ConvertDynamicJsonToDictionary(hotKeyMapping) : new Dictionary<string, string>();

                    // Convert ControllerInfo
                    var controllerInfo = item.GetObject("ControllerInfo");
                    var controllerInfoDict = controllerInfo != null ? ConvertDynamicJsonToDictionary(controllerInfo) : new Dictionary<string, string>();

                    controllers.Add(new N64Controller(emulator, name, guid, mappingDict, hotKeyMappingDict, controllerInfoDict));
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
}
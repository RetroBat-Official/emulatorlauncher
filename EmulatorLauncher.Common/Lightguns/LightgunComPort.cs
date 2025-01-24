using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;

namespace EmulatorLauncher.Common.Lightguns
{
    public class LightgunComPort
    {
        private static Dictionary<string, string> _cache;

        public static Dictionary<string, string> GetLightgunComPorts()
        {
            if (_cache == null)
                _cache = GetLightgunComPortsInternal();

            return _cache;
        }

        private static Dictionary<string, string> GetLightgunComPortsInternal()
        {
            var comPorts = new Dictionary<string, string>();

            try
            {
                SimpleLogger.Instance.Info("[INFO] Scanning for lightgun COM ports...");
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE ClassGuid=\"{4d36e978-e325-11ce-bfc1-08002be10318}\""))
                {
                    var ports = searcher.Get();

                    foreach (var port in ports)
                    {
                        string deviceId = port["DeviceID"]?.ToString();
                        string name = port["Name"]?.ToString();

                        if (string.IsNullOrEmpty(deviceId) || string.IsNullOrEmpty(name) || !name.Contains("COM"))
                            continue;

                        // Extract COM port number
                        string comPort = name.Split('(', ')').FirstOrDefault(x => x.Contains("COM"))?.Trim();
                        if (string.IsNullOrEmpty(comPort))
                            continue;

                        // Check if this is a lightgun device
                        var gunType = GetLightgunType(deviceId);
                        if (gunType != null)
                        {
                            SimpleLogger.Instance.Info($"[INFO] Found {gunType} on {comPort} (DeviceID: {deviceId})");
                            comPorts[deviceId] = comPort;
                        }
                    }
                }

                if (comPorts.Count == 0)
                    SimpleLogger.Instance.Info("[INFO] No lightgun COM ports found");
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[ERROR] Error getting COM ports: " + ex.Message);
            }

            return comPorts;
        }

        private static string GetLightgunType(string deviceId)
        {
            // Gun4IR
            if (deviceId.Contains("VID_2341") && (
                deviceId.Contains("PID_8042") || deviceId.Contains("PID_8043") ||
                deviceId.Contains("PID_8044") || deviceId.Contains("PID_8045") ||
                deviceId.Contains("PID_8046") || deviceId.Contains("PID_8047")))
                return "Gun4IR";

            // RetroShooter
            if (deviceId.Contains("VID_0483") && (
                deviceId.Contains("PID_5750") || deviceId.Contains("PID_5751") ||
                deviceId.Contains("PID_5752") || deviceId.Contains("PID_5753")))
                return "RetroShooter";

            return null;
        }

        private static bool IsLightgunDevice(string deviceId)
        {
            return GetLightgunType(deviceId) != null;
        }

        public static void UpdateMameHookerConfig(string configPath, List<string> comPorts)
        {
            if (!Directory.Exists(configPath))
            {
                SimpleLogger.Instance.Error($"[ERROR] MameHooker config path not found: {configPath}");
                return;
            }

            if (comPorts == null || comPorts.Count == 0)
            {
                SimpleLogger.Instance.Warning("[WARNING] No COM ports provided for configuration");
                return;
            }

            SimpleLogger.Instance.Info($"[INFO] Updating MameHooker config files in: {configPath}");
            SimpleLogger.Instance.Info($"[INFO] Using COM ports: {string.Join(", ", comPorts)}");

            string iniPath = Path.Combine(configPath, "ini");
            SimpleLogger.Instance.Info($"[INFO] Checking ini directory: {iniPath}");

            // Create ini directory structure if it doesn't exist
            try
            {
                if (!Directory.Exists(iniPath))
                {
                    SimpleLogger.Instance.Info($"[INFO] Creating ini directory: {iniPath}");
                    Directory.CreateDirectory(iniPath);
                }

                string mamePath = Path.Combine(iniPath, "MAME");
                if (!Directory.Exists(mamePath))
                {
                    SimpleLogger.Instance.Info($"[INFO] Creating MAME directory: {mamePath}");
                    Directory.CreateDirectory(mamePath);
                }

                string supermodelPath = Path.Combine(iniPath, "SUPERMODEL");
                if (!Directory.Exists(supermodelPath))
                {
                    SimpleLogger.Instance.Info($"[INFO] Creating SUPERMODEL directory: {supermodelPath}");
                    Directory.CreateDirectory(supermodelPath);
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error($"[ERROR] Failed to create directory structure: {ex.Message}");
                return;
            }

            string[] configFiles = new string[]
            {
                Path.Combine(iniPath, "default.ini"),
                Path.Combine(iniPath, "MAME", "default.ini"),
                Path.Combine(iniPath, "SUPERMODEL", "default.ini")
            };

            foreach (string configFile in configFiles)
            {
                SimpleLogger.Instance.Info($"[INFO] Processing config file: {configFile}");

                try
                {
                    string directory = Path.GetDirectoryName(configFile);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                        SimpleLogger.Instance.Info($"[INFO] Created directory: {directory}");
                    }

                    // Create or update the config file
                    var ini = new MameHookerIni(configFile);
                    ini.UpdateMameHookerConfig(comPorts);
                    SimpleLogger.Instance.Info($"[INFO] Successfully updated config file: {configFile}");
                }
                catch (Exception ex)
                {
                    SimpleLogger.Instance.Error($"[ERROR] Failed to update config file {configFile}: {ex.Message}");
                }
            }
        }
    }
} 
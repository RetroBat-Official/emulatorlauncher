using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace EmulatorLauncher.Common.FileFormats
{
    public class MameHookerIni
    {
        private readonly string _filePath;
        private readonly Dictionary<string, string> _settings;
        private readonly List<string> _lines;

        public MameHookerIni(string filePath)
        {
            _filePath = filePath;
            _settings = new Dictionary<string, string>();
            _lines = new List<string>();

            if (File.Exists(filePath))
                Load();
        }

        private void Load()
        {
            try
            {
                _lines.Clear();
                _settings.Clear();

                var lines = File.ReadAllLines(_filePath);
                foreach (var line in lines)
                {
                    _lines.Add(line);

                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                        continue;

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        string value = parts[1].Trim();
                        _settings[key] = value;
                    }
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[MameHookerIni] Error loading file: " + ex.Message);
            }
        }

        public void Save()
        {
            try
            {
                var updatedLines = new List<string>();
                bool[] lineProcessed = new bool[_lines.Count];

                // First, update existing lines
                for (int i = 0; i < _lines.Count; i++)
                {
                    string line = _lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                    {
                        updatedLines.Add(line);
                        continue;
                    }

                    var parts = line.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim();
                        if (_settings.TryGetValue(key, out string value))
                        {
                            updatedLines.Add($"{key}={value}");
                            lineProcessed[i] = true;
                        }
                        else
                            updatedLines.Add(line);
                    }
                    else
                        updatedLines.Add(line);
                }

                // Then add any new settings
                foreach (var kvp in _settings)
                {
                    if (!_lines.Any(l => l.StartsWith(kvp.Key + "=")))
                        updatedLines.Add($"{kvp.Key}={kvp.Value}");
                }

                File.WriteAllLines(_filePath, updatedLines);
            }
            catch (Exception ex)
            {
                SimpleLogger.Instance.Error("[MameHookerIni] Error saving file: " + ex.Message);
            }
        }

        public string GetValue(string key)
        {
            return _settings.TryGetValue(key, out string value) ? value : null;
        }

        public void SetValue(string key, string value)
        {
            _settings[key] = value;
        }

        public void UpdateMameHookerConfig(List<string> comPorts)
        {
            if (comPorts == null || comPorts.Count == 0)
                return;

            SimpleLogger.Instance.Info($"[INFO] Updating MameHooker config with ports: {string.Join(", ", comPorts)}");

            // Build MameStart line
            string mameStart = "";
            for (int p = 0; p < comPorts.Count && p < 4; p++)
            {
                string portNumber = comPorts[p].Replace("COM", "");
                if (p > 0) mameStart += ", ";
                mameStart += $"cmo {portNumber} baud=9600_parity=N_data=8_stop=1";
            }
            for (int p = 0; p < comPorts.Count && p < 4; p++)
            {
                string portNumber = comPorts[p].Replace("COM", "");
                mameStart += $", cmw {portNumber} S6M1x2xM3x0xM8m0x";
            }
            SetValue("MameStart", mameStart);
            SimpleLogger.Instance.Info($"[INFO] Setting MameStart: {mameStart}");

            // Build MameStop line
            string mameStop = "";
            for (int p = 0; p < comPorts.Count && p < 4; p++)
            {
                string portNumber = comPorts[p].Replace("COM", "");
                if (p > 0) mameStop += ", ";
                mameStop += $"cmw {portNumber} E";
            }
            for (int p = 0; p < comPorts.Count && p < 4; p++)
            {
                string portNumber = comPorts[p].Replace("COM", "");
                mameStop += $", cmc {portNumber}";
            }
            SetValue("MameStop", mameStop);
            SimpleLogger.Instance.Info($"[INFO] Setting MameStop: {mameStop}");

            Save();
        }
    }
} 
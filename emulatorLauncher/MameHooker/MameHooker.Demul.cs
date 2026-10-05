using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.Lightguns;

namespace EmulatorLauncher
{
    partial class MameHooker
    {
        public static class Demul
        {
            private static readonly HashSet<string> SupportedGames = new HashSet<string>
            {
                "braveff",    // Brave Fire Fighters
                "confmiss",   // Confidential Mission
                "deathcox",   // Death Crimson OX
                "hotd2",      // House of the Dead 2
                "hotd2o",     // House of the Dead 2
                "hotd2p",     // House of the Dead 2 Proto
                "lupinsho",   // Lupin the Third
                "manicpnc",   // Manic Panic Ghosts
                "rangrmsn",   // Ranger Mission
                "sprtshot",   // Sports Shooting USA
                "xtrmhunt"    // Extreme Hunting
            };

            public static bool ConfigureDemul(string romName)
            {
                // Check if this is a supported lightgun game
                if (!SupportedGames.Contains(romName))
                {
                    SimpleLogger.Instance.Info("[INFO] Not a supported Demul lightgun game, skipping configuration");
                    return false;
                }

                // Get MameHooker path first to verify it exists
                if (!GetMameHookerExecutable(out string executable, out string mamehookPath))
                {
                    SimpleLogger.Instance.Warning("[WARNING] MameHooker path not found");
                    return false;
                }

                SimpleLogger.Instance.Info($"[INFO] MameHooker path: {mamehookPath}");

                // Configure lightgun COM ports
                var comPorts = LightgunComPort.GetLightgunComPorts();
                var activePorts = LightgunComPort.GetOrderedComPorts(comPorts);

                if (activePorts.Any())
                {
                    // Update default configuration files first
                    LightgunComPort.UpdateMameHookerConfig(mamehookPath, activePorts);

                    // Look for game specific INI in MAME and SUPERMODEL folders
                    string[] folders = { "MAME", "SUPERMODEL" };
                    foreach (string folder in folders)
                    {
                        string iniPath = Path.Combine(mamehookPath, "ini", folder, romName + ".ini");
                        SimpleLogger.Instance.Info($"[INFO] Looking for game-specific INI file: {iniPath}");
                        if (File.Exists(iniPath))
                        {
                            SimpleLogger.Instance.Info($"[INFO] Found game-specific INI file: {iniPath}");
                            try
                            {
                                string[] lines = File.ReadAllLines(iniPath);
                                bool modified = false;

                                for (int i = 0; i < lines.Length; i++)
                                {
                                    string line = lines[i];
                                    
                                    // Process each line that needs port number update
                                    if (line.Contains("P1_CtmRecoil=") || 
                                        line.Contains("P1_GunRecoil=") ||
                                        line.Contains("P1_Damaged="))
                                    {
                                        if (activePorts.Count > 0)
                                            lines[i] = UpdatePortNumber(line, activePorts[0]);
                                        modified = true;
                                    }
                                    else if (line.Contains("P2_CtmRecoil=") || 
                                           line.Contains("P2_GunRecoil=") ||
                                           line.Contains("P2_Damaged="))
                                    {
                                        if (activePorts.Count > 1)
                                            lines[i] = UpdatePortNumber(line, activePorts[1]);
                                        modified = true;
                                    }
                                }

                                if (modified)
                                {
                                    File.WriteAllLines(iniPath, lines);
                                    SimpleLogger.Instance.Info($"[INFO] Updated {folder} INI file for {romName}");
                                }
                            }
                            catch (Exception ex)
                            {
                                SimpleLogger.Instance.Error($"[ERROR] Failed to update {folder} INI file: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    SimpleLogger.Instance.Warning("[WARNING] No lightgun COM ports found to configure");
                    return false;
                }

                return true;
            }
        }
    }
} 
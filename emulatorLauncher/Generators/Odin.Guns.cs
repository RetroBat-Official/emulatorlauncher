using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Lightguns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EmulatorLauncher
{
    partial class OdinGenerator
    {
        // Games with a sight aimed by a light gun or the mouse
        private static readonly string[] _gunGames = { "braveff" };

        private List<RawLightgun> _guns = new List<RawLightgun>();
        private bool _gunInvert;

        /// <summary>
        /// Light guns of Brave Firefighters : [players.<game>] gun1 / gun2, the Raw Input identity Odin gives the device (rawinput.cpp, open_device).
        /// Without a gun, the mouse aims for player 1 (Odin default). The gun buttons are the mouse buttons of the lines of the player :
        /// water = left (trigger), fog = right, start = middle.
        /// </summary>
        private void SetupGuns(IniFile ini)
        {
            string section = "players." + _game;

            ini.Remove(section, "gun1");
            ini.Remove(section, "gun2");

            if (!_gunGames.Contains(_game))
                return;

            // The mouse or the gun aims in the game
            ini.Remove(section, "light_gun_mode");

            var all = RawLightgun.GetRawLightguns();

            if (SystemConfig.getOptBoolean("use_guns"))
            {
                _guns = all.Where(g => g.Type != RawLighGunType.Mouse && !string.IsNullOrEmpty(g.DevicePath)).ToList();

                if (SystemConfig.getOptBoolean("one_gun"))
                    _guns = _guns.Take(1).ToList();
                else
                    _guns = _guns.Take(2).ToList();
            }

            _gunInvert = _guns.Count > 0 && SystemConfig.getOptBoolean("gun_invert");

            for (int i = 0; i < _guns.Count; i++)
            {
                string identity = GetGunIdentity(_guns[i], all);
                ini.WriteValue(section, "gun" + (i + 1), identity + " " + _guns[i].Name);
                SimpleLogger.Instance.Info("[GUNS] Player " + (i + 1) + " : " + _guns[i].Name + " (" + _guns[i].Type + ") => " + identity);
            }

            // Sinden : white border drawn by Odin around the game
            bool sinden = _guns.Any(g => g.Type == RawLighGunType.SindenLightgun);
            if (sinden)
            {
                Guns.StartSindenSoftware();
                ini.WriteValue(section, "sinden_border", "on");
                BindIniFeature(ini, section, "sinden_border_size", "odin_sinden_border", "2");
            }
            else
            {
                ini.Remove(section, "sinden_border");
                ini.Remove(section, "sinden_border_size");
            }
        }

        /// <summary>
        /// Identity of a gun in Odin : "vidpid:vvvv:pppp" for the first mouse of that VID:PID in the Raw Input list,
        /// "path:" + the end of the device path for the next ones (two guns of a kind). RetroBat lists the mice in the same order (GetRawInputDeviceList).
        /// </summary>
        private static string GetGunIdentity(RawLightgun gun, RawLightgun[] all)
        {
            string path = gun.DevicePath;

            int vid, pid;
            if (GetVidPid(path, out vid, out pid))
            {
                bool first = !all.Any(m => m != gun && m.Index < gun.Index && GetVidPid(m.DevicePath, out int v, out int p) && v == vid && p == pid);
                if (first)
                    return "vidpid:" + vid.ToString("x4") + ":" + pid.ToString("x4");
            }

            // Odin removes the \\?\ prefix and the interface GUID, and keeps the last 56 characters
            if (path.StartsWith("\\\\?\\") || path.StartsWith("\\\\.\\"))
                path = path.Substring(4);

            int guid = path.IndexOf("#{");
            if (guid >= 0)
                path = path.Substring(0, guid);

            return "path:" + (path.Length > 56 ? path.Substring(path.Length - 56) : path);
        }

        // Odin reads the first "VID_" and "PID_" of the path (vidpid_of), none for Bluetooth devices
        private static bool GetVidPid(string path, out int vid, out int pid)
        {
            vid = pid = 0;
            if (string.IsNullOrEmpty(path))
                return false;

            int v = path.IndexOf("VID_", StringComparison.Ordinal);
            int p = path.IndexOf("PID_", StringComparison.Ordinal);
            if (v < 0 || p < 0 || v + 8 > path.Length || p + 8 > path.Length)
                return false;

            return int.TryParse(path.Substring(v + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out vid) &&
                   int.TryParse(path.Substring(p + 4, 4), System.Globalization.NumberStyles.HexNumber, null, out pid) &&
                   vid != 0 && pid != 0;
        }

        /// <summary>
        /// Keyboard and mouse inputs of a control with the guns : trigger and reload swapped (gun_invert),
        /// DolphinBar Wiimotes send keys for the D-pad : first Wiimote Left = coin, Right = start, second Wiimote Down = coin, Up = start.
        /// </summary>
        private string GetGunInputs(string id, string keys)
        {
            if (_gunInvert && keys != null)
            {
                if (id.EndsWith("_water"))
                    keys = keys.Replace("mouse:left", "mouse:right");
                else if (id.EndsWith("_fog"))
                    keys = keys.Replace("mouse:right", "mouse:left");
            }

            for (int i = 0; i < _guns.Count; i++)
            {
                if (_guns[i].Type != RawLighGunType.MayFlashWiimote)
                    continue;

                string start = i == 0 ? "1p_start" : "2p_start";
                string coin = i == 0 ? "coin1" : "coin2";

                if (id == start)
                    keys = AddInput(keys, i == 0 ? "key:Right" : "key:Up");
                else if (id == coin)
                    keys = AddInput(keys, i == 0 ? "key:Left" : "key:Down");
            }

            return keys;
        }

        private static string AddInput(string inputs, string input)
        {
            return string.IsNullOrEmpty(inputs) ? input : inputs + ", " + input;
        }

        /// <summary>
        /// With a DolphinBar, the arrow keys are Wiimote buttons : they no longer move the sight of player 1.
        /// </summary>
        private void WriteGunKeyboardLines(IniFile ini, string controlsSection)
        {
            if (!_guns.Any(g => g.Type == RawLighGunType.MayFlashWiimote))
                return;

            foreach (string half in new[] { "1p_sight_x_left", "1p_sight_x_right", "1p_sight_y_up", "1p_sight_y_down" })
                ini.WriteValue(controlsSection, half, "");
        }
    }
}
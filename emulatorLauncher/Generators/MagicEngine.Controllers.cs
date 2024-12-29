using System.IO;
using EmulatorLauncher.Common;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using System.Collections.Generic;
using EmulatorLauncher.Common.Joysticks;

namespace EmulatorLauncher
{
    partial class MagicEngineGenerator : Generator
    {
        private void SetupControllers(string path)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
                return;

            SimpleLogger.Instance.Info("[INFO] Creating controller configuration for MagicEngine");

            string cfgFile = Path.Combine(path, "pce.cfg");

            byte[] bytes;

            SimpleLogger.Instance.Info("Configuring input file " + cfgFile);

            var controllers = Program.Controllers.Where(c => c.Config != null && !c.IsKeyboard).ToList();
            int controllerNB = controllers.Count;

            if (controllerNB == 0)
                return;

            if (!File.Exists(cfgFile))
            {
                string toCopy = Path.Combine(AppConfig.GetFullPath("templates"), "magicengine", "pce_empty.cfg");

                if (!File.Exists(toCopy))
                    return;

                bytes = File.ReadAllBytes(toCopy);
            }
            else
                bytes = File.ReadAllBytes(cfgFile);

            byte[] gamepadPattern = new byte[] { 0x47, 0x41, 0x4D, 0x45, 0x50, 0x41, 0x44 };
            int gamepadByteIndex = bytes.IndexOf(gamepadPattern);
            int byteLength = bytes.Length;
            int gamepadLength = bytes.Length - gamepadByteIndex;
            int targetLength = 228;

            if (controllerNB > 4)
                targetLength = 756;
            else
                targetLength = (controllerNB * 132) + 96;

            if (gamepadByteIndex == -1)
                return;

            byte[] newBytes = new byte[gamepadByteIndex + targetLength];
            int bytesToCopy = gamepadByteIndex + 11;
            System.Array.Copy(bytes, newBytes, bytesToCopy);
            newBytes[gamepadByteIndex + 10] = 0x01;

            foreach (var controller in controllers.OrderBy(i => i.PlayerIndex).Take(5))
                WriteControllerBytes(newBytes, controller, gamepadByteIndex);

            File.WriteAllBytes(cfgFile, newBytes);
        }

        private void WriteControllerBytes(byte[] bytes, Controller ctrl, int gamepadByteIndex)
        {
            if (ctrl == null)
                return;

            InputConfig joy = ctrl.Config;
            if (joy == null)
                return;

            string gamecontrollerDB = Path.Combine(AppConfig.GetFullPath("tools"), "gamecontrollerdb.txt");
            string guid = (ctrl.Guid.ToString()).Substring(0, 24) + "00000000";
            SdlToDirectInput sdlCtrl = null;
            int playerIndex = ctrl.PlayerIndex - 1;
            int startIndex = gamepadByteIndex + 16 + (playerIndex * 148);
            int ctrlIndex = ctrl.DirectInput != null ? ctrl.DirectInput.DeviceIndex : ctrl.DeviceIndex;

            string forceIndex = "magicengine_p" + ctrl.PlayerIndex + "_forceindex";
            if (SystemConfig.isOptSet(forceIndex) && !string.IsNullOrEmpty(SystemConfig[forceIndex]))
            {
                ctrlIndex = SystemConfig[forceIndex].ToInteger();
            }

            if (playerIndex == 0 && ctrlIndex > 0)
                startIndex = startIndex + 16;

            int buttonStart = startIndex + 4;

            if (!File.Exists(gamecontrollerDB))
            {
                SimpleLogger.Instance.Error("[INFO] gamecontrollerdb.txt not found, not possible to autoconfigure gamepad for Player " + ctrl.PlayerIndex.ToString());
                return;
            }

            try
            {
                sdlCtrl = GameControllerDBParser.ParseByGuid(gamecontrollerDB, guid);
            }
            catch { }

            if (sdlCtrl == null)
            {
                SimpleLogger.Instance.Error("[INFO] No gamepad found for GUID " + guid + ", not possible to autoconfigure gamepad for Player " + ctrl.PlayerIndex.ToString());
                return;
            }

            bytes[startIndex] = (byte)playerIndex;

            foreach (string button in buttonMap.Keys)
            {
                byte[] toSet = GetButtonByteArray(sdlCtrl, button, ctrlIndex);

                if (!IsZeroByteArray(toSet))
                {
                    System.Array.Copy(toSet, 0, bytes, buttonStart, 12);
                    buttonStart += 12;
                }
            }

            SimpleLogger.Instance.Info("[INFO] Assigned controller " + ctrl.DevicePath + " to player : " + ctrl.PlayerIndex.ToString());
        }

        private byte[] GetButtonByteArray(SdlToDirectInput sdlCtrl, string key, int ctrlIndex)
        {
            byte[] toSet = new byte[12];

            if (!sdlCtrl.ButtonMappings.ContainsKey(key))
                toSet = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00};

            string button = sdlCtrl.ButtonMappings[key];

            if (button.StartsWith("b"))
            {
                int buttonID = button.Substring(1).ToInteger();

                toSet[1] = toSet[2] = toSet[3] = toSet[5] = toSet[6] = toSet[7] = toSet[11] = 0x00;
                toSet[0] = 0x03;
                toSet[4] = (byte)buttonID;
                toSet[8] = buttonMap[key];
                toSet[9] = 0x02;
                toSet[10] = (byte)ctrlIndex;
            }

            else if (button.StartsWith("h"))
            {
                toSet[1] = toSet[2] = toSet[3] = toSet[5] = toSet[6] = toSet[7] = toSet[11] = 0x00;
                toSet[0] = 0x02;
                toSet[4] = 0x00;
                toSet[8] = buttonMap[key];
                toSet[9] = 0x02;
                toSet[10] = (byte)ctrlIndex;
            }

            else if (button.StartsWith("a") || button.StartsWith("+a") || button.StartsWith("-a"))
            {
                int axisID = button.Substring(1).ToInteger();

                if (button.StartsWith("-a") || button.StartsWith("+a"))
                    axisID = button.Substring(2).ToInteger();

                toSet[1] = toSet[2] = toSet[3] = toSet[5] = toSet[6] = toSet[7] = toSet[11] = 0x00;
                toSet[0] = 0x01;
                toSet[4] = (byte)axisID;
                toSet[8] = buttonMap[key];
                toSet[9] = 0x02;
                toSet[10] = (byte)ctrlIndex;
            }

            return toSet;
        }

        private static readonly Dictionary<string, byte> buttonMap = new Dictionary<string, byte>()
        {
            { "leftx", 0x0D },
            { "lefty", 0x0E },
            { "a", 0x03 },
            { "b", 0x04 },
            { "leftshoulder", 0x0A },
            { "rightshoulder", 0x09 },
            { "y", 0x0B },
            { "x", 0x0C },
            { "back", 0x02 },
            { "start", 0x01 },
            { "dpup", 0x0F },
        };

        public static bool IsZeroByteArray(byte[] array)
        {
            foreach (byte b in array)
            {
                if (b != (byte)0x00)
                {
                    return false;
                }
            }
            return true;
        }
    }
}

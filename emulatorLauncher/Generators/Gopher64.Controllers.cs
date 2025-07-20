using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using EmulatorLauncher.Common;
using EmulatorLauncher.Common.FileFormats;
using EmulatorLauncher.Common.Joysticks;
using System.Linq;
using EmulatorLauncher.Common.EmulationStation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static EmulatorLauncher.PadToKeyboard.SendKey;
using System.Windows.Input;

namespace EmulatorLauncher
{
    partial class Gopher64Generator : Generator
    {
        private void ConfigureControls(JObject input, JObject profiles)
        {
            if (Program.SystemConfig.isOptSet("disableautocontrollers") && Program.SystemConfig["disableautocontrollers"] == "1")
            {
                SimpleLogger.Instance.Info("[INFO] Auto controller configuration disabled.");
                return;
            }

            // Cleanup all
            profiles.Property("RetroBat1")?.Remove();
            profiles.Property("RetroBat2")?.Remove();
            profiles.Property("RetroBat3")?.Remove();
            profiles.Property("RetroBat4")?.Remove();

            JArray bindingArray = new JArray("default", "default", "default", "default");
            input["input_profile_binding"] = bindingArray;

            JArray controllerAssignment = new JArray(null, null, null, null);
            input["controller_assignment"] = controllerAssignment;

            JArray enableControllers = new JArray(false, false, false, false);
            input["controller_enabled"] = enableControllers;

            JArray transferPack = new JArray(false, false, false, false);
            input["transfer_pak"] = transferPack;

            bindingArray = new JArray();
            enableControllers = new JArray();
            controllerAssignment = new JArray();

            foreach (var controller in this.Controllers.Where(c => !c.IsKeyboard).OrderBy(i => i.PlayerIndex).Take(4))
            {
                int index = controller.PlayerIndex;
                string profile = "RetroBat" + index.ToString();

                if (profiles[profile] == null || profiles[profile].Type != JTokenType.Object)
                    profiles[profile] = new JObject();
                var cProfile = (JObject)profiles[profile];

                ConfigureInput(cProfile, controller, controllerAssignment);

                bindingArray.Add(profile);
                enableControllers.Add(true);
            }

            // Fill next sections
            while (bindingArray.Count > 4)
                bindingArray.RemoveAt(bindingArray.Count - 1);
            while (bindingArray.Count < 4)
                bindingArray.Add("default");
            input["input_profile_binding"] = bindingArray;

            while (controllerAssignment.Count > 4)
                controllerAssignment.RemoveAt(controllerAssignment.Count - 1);
            while (controllerAssignment.Count < 4)
                controllerAssignment.Add(null);
            input["controller_assignment"] = controllerAssignment;

            while (enableControllers.Count > 4)
                enableControllers.RemoveAt(enableControllers.Count - 1);
            while (enableControllers.Count < 4)
                enableControllers.Add(false);
            input["controller_enabled"] = enableControllers;
        }

        private void ConfigureInput(JObject profile, Controller controller, JArray controllerAssignment)
        {
            string cPath = controller.DirectInput.DevicePath
                    .Replace("hid#", "HID#")
                    .Replace("_vid", "_VID")
                    .Replace("_pid", "_PID")
                    .Replace("vid_", "VID_")
                    .Replace("pid_", "PID_")
                    .Replace("ig_", "IG_")
                    ;

            if (controller.IsXInputDevice)
            {
                int xId = controller.XInput == null ? 0 : controller.XInput.DeviceIndex;
                if (xId >= 0)
                    cPath = "XInput#" + xId;
            }

            // Reset profile to default
            profile["keys"] = CreateKeySection(controller.PlayerIndex);
            profile["controller_buttons"] = CreateButtonSection();
            profile["controller_axis"] = CreateAxisSection();
            profile["joystick_buttons"] = CreateButtonSection();
            profile["joystick_hat"] = CreateAxisSection(true);
            profile["joystick_axis"] = CreateAxisSection();

            // Initialize arrays for buttons and axes
            JArray buttonArray = new JArray();
            JArray axisArray = new JArray();

            // Mappings
            var mapping = standardMapping;

            string mappingProfile = "gopher64_inputprofile" + controller.PlayerIndex.ToString();

            if (SystemConfig.isOptSet(mappingProfile) && !string.IsNullOrEmpty(SystemConfig[mappingProfile]))
            {
                string mappingName = SystemConfig[mappingProfile];

                switch (mappingName)
                {
                    case "c_stick_zl":
                        mapping = standardMapping;
                        break;
                    case "c_face_zl":
                        mapping = cFaceZLMapping;
                        break;
                    case "c_stick":
                        mapping = cStickMapping;
                        break;
                    case "c_face":
                        mapping = cFaceMapping;
                        break;
                    case "xbox":
                        mapping = xboxMapping;
                        break;
                }
            }

            foreach (var button in mapping)
            {
                var c = controller.Config;
                if (c == null)
                {
                    buttonArray.Add(new JObject
                    {
                        ["enabled"] = false,
                        ["id"] = 0
                    });
                    axisArray.Add(new JObject
                    {
                        ["enabled"] = false,
                        ["id"] = 0,
                        ["axis"] = 0
                    });
                    continue;
                }

                var input = controller.Config[button.Value];
                if (revertedAxis.ContainsKey(button.Value))
                {
                    input = controller.Config[revertedAxis[button.Value]];
                }
                
                if (input == null)
                {
                    buttonArray.Add(new JObject
                    {
                        ["enabled"] = false,
                        ["id"] = 0
                    });
                    axisArray.Add(new JObject
                    {
                        ["enabled"] = false,
                        ["id"] = 0,
                        ["axis"] = 0
                    });
                    continue;
                }

                switch (button.Value)
                {
                    case InputKey.left:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 13 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.right:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 14 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.up:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 11 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.down:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 12 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.start:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 6 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.l2:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 4, ["axis"] = 1 });
                        break;
                    case InputKey.r2:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 5, ["axis"] = 1 });
                        break;
                    case InputKey.select:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 4 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.a:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.b:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 1 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.y:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 2 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.x:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 3 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.pageup:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 9 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.pagedown:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 10 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.l3:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 7 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.r3:
                        buttonArray.Add(new JObject { ["enabled"] = true, ["id"] = 8 });
                        axisArray.Add(new JObject { ["enabled"] = false, ["id"] = 0, ["axis"] = 0 });
                        break;
                    case InputKey.leftanalogdown:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 1, ["axis"] = 1 });
                        break;
                    case InputKey.leftanalogup:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 1, ["axis"] = -1 });
                        break;
                    case InputKey.leftanalogleft:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 0, ["axis"] = -1 });
                        break;
                    case InputKey.leftanalogright:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 0, ["axis"] = 1 });
                        break;
                    case InputKey.rightanalogdown:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 3, ["axis"] = 1 });
                        break;
                    case InputKey.rightanalogup:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 3, ["axis"] = -1 });
                        break;
                    case InputKey.rightanalogleft:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 2, ["axis"] = -1 });
                        break;
                    case InputKey.rightanalogright:
                        buttonArray.Add(new JObject { ["enabled"] = false, ["id"] = 0 });
                        axisArray.Add(new JObject { ["enabled"] = true, ["id"] = 2, ["axis"] = 1 });
                        break;
                }
                profile["controller_buttons"] = buttonArray;
                profile["controller_axis"] = axisArray;

            }
            controllerAssignment.Add(cPath);
            profile["dinput"] = false;
        }

        private static Dictionary<InputKey, InputKey> revertedAxis = new Dictionary<InputKey, InputKey>()
        {
            { InputKey.leftanalogright, InputKey.leftanalogleft },
            { InputKey.leftanalogdown, InputKey.leftanalogup },
            { InputKey.rightanalogright, InputKey.rightanalogleft },
            { InputKey.rightanalogdown, InputKey.rightanalogup },
        };

        private JArray CreateKeySection(int index)
        {
            JArray keysArray;

            if (index == 1)
            {
                keysArray = new JArray(
                    new JObject { ["enabled"] = true, ["id"] = 7 },
                    new JObject { ["enabled"] = true, ["id"] = 4 },
                    new JObject { ["enabled"] = true, ["id"] = 22 },
                    new JObject { ["enabled"] = true, ["id"] = 26 },
                    new JObject { ["enabled"] = true, ["id"] = 40 },
                    new JObject { ["enabled"] = true, ["id"] = 29 },
                    new JObject { ["enabled"] = true, ["id"] = 224 },
                    new JObject { ["enabled"] = true, ["id"] = 225 },
                    new JObject { ["enabled"] = true, ["id"] = 15 },
                    new JObject { ["enabled"] = true, ["id"] = 13 },
                    new JObject { ["enabled"] = true, ["id"] = 14 },
                    new JObject { ["enabled"] = true, ["id"] = 12 },
                    new JObject { ["enabled"] = true, ["id"] = 6 },
                    new JObject { ["enabled"] = true, ["id"] = 27 },
                    new JObject { ["enabled"] = true, ["id"] = 80 },
                    new JObject { ["enabled"] = true, ["id"] = 79 },
                    new JObject { ["enabled"] = true, ["id"] = 82 },
                    new JObject { ["enabled"] = true, ["id"] = 81 },
                    new JObject { ["enabled"] = true, ["id"] = 54 }
                );
            }
            else
            {
                keysArray = new JArray(
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 },
                    new JObject { ["enabled"] = false, ["id"] = 0 }
                );
            }
            return keysArray;
        }

        private JArray CreateAxisSection(bool hat = false)
        {
            string axisType = hat ? "direction" : "axis";

            // Create the keys array
            JArray axisArray = new JArray(
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0, [axisType] = 0 }
            );
            return axisArray;
        }

        private JArray CreateButtonSection()
        {
            // Create the keys array
            JArray buttonArray = new JArray(
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 },
                new JObject { ["enabled"] = false, ["id"] = 0 }
            );
            return buttonArray;
        }

        private Dictionary<string, InputKey> standardMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.y },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Changepak", InputKey.r3 }
        };

        private Dictionary<string, InputKey> cFaceZLMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.r2 },
            { "A", InputKey.pagedown },
            { "CRight", InputKey.b },
            { "CLeft", InputKey.y },
            { "CDown", InputKey.a },
            { "CUp", InputKey.x },
            { "R", InputKey.pageup },
            { "L", InputKey.select },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Changepak", InputKey.r3 }
        };

        private Dictionary<string, InputKey> cStickMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.r2 },
            { "B", InputKey.y },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Changepak", InputKey.r3 }
        };

        private Dictionary<string, InputKey> cFaceMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.r2 },
            { "B", InputKey.l2 },
            { "A", InputKey.pageup },
            { "CRight", InputKey.b },
            { "CLeft", InputKey.y },
            { "CDown", InputKey.a },
            { "CUp", InputKey.x },
            { "R", InputKey.pagedown },
            { "L", InputKey.select },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Changepak", InputKey.r3 }
        };

        private Dictionary<string, InputKey> xboxMapping = new Dictionary<string, InputKey>
        {
            { "Right", InputKey.right },
            { "Left", InputKey.left },
            { "Down", InputKey.down },
            { "Up", InputKey.up },
            { "Start", InputKey.start },
            { "Z", InputKey.l2 },
            { "B", InputKey.b },
            { "A", InputKey.a },
            { "CRight", InputKey.rightanalogright },
            { "CLeft", InputKey.rightanalogleft },
            { "CDown", InputKey.rightanalogdown },
            { "CUp", InputKey.rightanalogup },
            { "R", InputKey.pagedown },
            { "L", InputKey.pageup },
            { "Stickleft", InputKey.leftanalogleft },
            { "Stickright", InputKey.leftanalogright },
            { "Stickup", InputKey.leftanalogup },
            { "Stickdown", InputKey.leftanalogdown },
            { "Changepak", InputKey.r3 }
        };
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace EmulatorLauncher.Common.Joysticks
{
    /// <summary>
    /// Extract guids from a specific SDL2.dll version. Guids can be retrieved using physical devices (HID) paths ( see SDL_JoystickPathForIndex )
    /// </summary>
    public class SdlDllControllersMapping
    {
        public static SdlDllControllersMapping FromSdlVersion(SdlVersion version, string hints = null)
        {
            if (version == SdlVersion.Unknown || version == SdlVersion.SDL2_0_X)
                return null;

            string currentPath = Path.GetDirectoryName(typeof(SdlDllControllersMapping).Assembly.Location);

            string currentSdlPath = Path.Combine(currentPath, "SDL2.dll");

            var sdlVersion = SdlJoystickGuidManager.GetSdlVersion(currentSdlPath);
            if (sdlVersion == version)
            {
                var ret = FromDll(currentSdlPath, hints);
                if (ret != null)
                    return ret;
            }

            string ver =
                version == SdlVersion.SDL2_24 ? "2.24.0.0" :
                version == SdlVersion.SDL2_26 ? "2.26.0.0" :
                version == SdlVersion.SDL2_30 ? "2.30.0.0" :
                null;

            if (ver == null)
                return null;

            string sourceSDL = Path.GetFullPath(Path.Combine(currentPath, "sdl2", "SDL2_" + ver + ".dll"));
            if (File.Exists(sourceSDL))
                return FromDll(sourceSDL, hints);

            return null;
        }

        public static SdlDllControllersMapping FromDll(string path, string hints = null)
        {
            return new SdlDllControllersMapping(path, hints);
        }

        public SdlJoystickGuid GetControllerGuid(string hidPath)
        {
            SdlJoystickGuid ret;
            if (Mapping.TryGetValue(hidPath, out ret))
                return ret;

            var shortenPath = InputDevices.ShortenDevicePath(hidPath);
            if (shortenPath != hidPath)
            {
                if (Mapping.TryGetValue(shortenPath, out ret))
                    return ret;
            }

            var parentPath = InputDevices.GetInputDeviceParent(hidPath);
            if (parentPath != null)
            {
                if (Mapping.TryGetValue(parentPath, out ret))
                    return ret;

                shortenPath = InputDevices.ShortenDevicePath(parentPath);
                if (shortenPath != parentPath)
                {
                    if (Mapping.TryGetValue(shortenPath, out ret))
                        return ret;
                }
            }

            return null;
        }

        public Dictionary<string, SdlJoystickGuid> Mapping { get { return _mapping; } }

        #region Private
        private Dictionary<string, SdlJoystickGuid> _mapping = new Dictionary<string, SdlJoystickGuid>(StringComparer.OrdinalIgnoreCase);

        private SdlDllControllersMapping(string path, string hints = null)
        {
            int ticks = Environment.TickCount;

            if (!System.IO.File.Exists(path))
                return;

            var hModule = LoadLibrary(path);
            if (hModule == IntPtr.Zero)
                return;

            SDL_JoystickPathForIndex = (SDL_JoystickPathForIndexPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_JoystickPathForIndex"), typeof(SDL_JoystickPathForIndexPtr));
            if (SDL_JoystickPathForIndex == null)
                return;

            SDL_Init = (SDL_InitPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_Init"), typeof(SDL_InitPtr));
            SDL_InitSubSystem = (SDL_InitSubSystemPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_InitSubSystem"), typeof(SDL_InitSubSystemPtr));
            SDL_QuitSubSystem = (SDL_QuitSubSystemPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_QuitSubSystem"), typeof(SDL_QuitSubSystemPtr));
            SDL_Quit = (SDL_QuitPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_Quit"), typeof(SDL_QuitPtr));
            SDL_NumJoysticks = (SDL_NumJoysticksPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_NumJoysticks"), typeof(SDL_NumJoysticksPtr));
            SDL_JoystickGetDeviceGUID = (SDL_JoystickGetDeviceGUIDPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_JoystickGetDeviceGUID"), typeof(SDL_JoystickGetDeviceGUIDPtr));
            SDL_SetHint = (SDL_SetHintPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_SetHint"), typeof(SDL_SetHintPtr));
            SDL_GameControllerNameForIndex = (SDL_GameControllerNameForIndexPtr)Marshal.GetDelegateForFunctionPointer(GetProcAddress(hModule, "SDL_GameControllerNameForIndex"), typeof(SDL_GameControllerNameForIndexPtr));

            LoadControllers(hints);

            FreeLibrary(hModule);

            Debug.WriteLine("[SDLControllersMapping] " + (Environment.TickCount - ticks).ToString() + " ms");
        }

        private void LoadControllers(string hints)
        {
            if (hints != null)
            {
                foreach (var hint in hints.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var keyValue = hints.Split(new char[] { '=' });
                    if (keyValue.Length == 2)
                        SDL_SetHint(SDL.UTF8_ToNative(keyValue[0].Trim()), SDL.UTF8_ToNative(keyValue[1].Trim()));
                }
            }

            SDL_Init(SDL.SDL_INIT_JOYSTICK);
            SDL_InitSubSystem(SDL.SDL_INIT_JOYSTICK);

            int numJoysticks = SDL_NumJoysticks();

            for (int i = 0; i < numJoysticks; i++)
            {
                string hidpath = Marshal.PtrToStringAnsi(SDL_JoystickPathForIndex(i));

                if (string.IsNullOrEmpty(hidpath))
                    continue;

                var guid = new SdlJoystickGuid(SDL_JoystickGetDeviceGUID(i));
                var name = Marshal.PtrToStringAnsi(SDL_GameControllerNameForIndex(i));

                _mapping[hidpath] = guid;

                var parentPath = InputDevices.GetInputDeviceParent(hidpath);

                var shortenPath = InputDevices.ShortenDevicePath(parentPath);
                if (shortenPath != hidpath)
                    _mapping[parentPath] = guid;
            }

            SDL_QuitSubSystem(SDL.SDL_INIT_JOYSTICK);
            SDL_Quit();
        }
        #endregion

        #region Windows Apis
        [DllImport("kernel32.dll")]
        static extern IntPtr LoadLibrary(string dllToLoad);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);

        [DllImport("kernel32.dll")]
        static extern bool FreeLibrary(IntPtr hModule);
        #endregion

        #region SDL Apis
        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate int SDL_InitPtr(uint flags);
        SDL_InitPtr SDL_Init;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate int SDL_InitSubSystemPtr(uint flags);
        SDL_InitSubSystemPtr SDL_InitSubSystem;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate void SDL_QuitPtr();
        SDL_QuitPtr SDL_Quit;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate void SDL_QuitSubSystemPtr(uint flags);
        SDL_QuitSubSystemPtr SDL_QuitSubSystem;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate int SDL_NumJoysticksPtr();
        SDL_NumJoysticksPtr SDL_NumJoysticks;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate Guid SDL_JoystickGetDeviceGUIDPtr(int device_index);
        SDL_JoystickGetDeviceGUIDPtr SDL_JoystickGetDeviceGUID;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate IntPtr SDL_JoystickPathForIndexPtr(int device_index);
        SDL_JoystickPathForIndexPtr SDL_JoystickPathForIndex;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate IntPtr SDL_GameControllerNameForIndexPtr(int joystick_index);
        SDL_GameControllerNameForIndexPtr SDL_GameControllerNameForIndex;

        [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Cdecl)]
        delegate SDL.SDL_bool SDL_SetHintPtr(byte[] name, byte[] value);
        SDL_SetHintPtr SDL_SetHint;
        #endregion
    }

}

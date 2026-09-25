using EmulatorLauncher.Common;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace EmulatorLauncher
{
    /// <summary>
    /// Turns a launch failure into a short, actionable message for the end user.
    /// The message is written to launch_error.log by Generator.SetCustomError() and displayed
    /// by EmulationStation when EmulatorLauncher returns exit code 299 (CustomError).
    ///
    /// Every message is built on the same 3 lines, so the user always knows what to do next :
    ///     &lt;what failed&gt;
    ///     Cause: &lt;why, in plain words&gt;
    ///     What to do: &lt;one concrete action&gt;
    ///
    /// FromException and FromExitCode are emulator agnostic : they only use what Windows reports
    /// about the process, and therefore work for every generator without any per-emulator work.
    /// FromMameExitCode is the first emulator specific entry : MAME publishes structured exit codes.
    /// Generators that build their own message must use Format() so every message looks the same.
    /// </summary>
    static class LaunchDiagnostics
    {
        #region Win32 error codes - thrown by Process.Start
        private const int ERROR_FILE_NOT_FOUND = 2;
        private const int ERROR_PATH_NOT_FOUND = 3;
        private const int ERROR_ACCESS_DENIED = 5;
        private const int ERROR_NOT_ENOUGH_MEMORY = 8;
        private const int ERROR_SHARING_VIOLATION = 32;
        private const int ERROR_MOD_NOT_FOUND = 126;
        private const int ERROR_BAD_EXE_FORMAT = 193;
        private const int ERROR_ELEVATION_REQUIRED = 740;
        private const int ERROR_DLL_INIT_FAILED = 1114;
        private const int ERROR_CANCELLED = 1223;
        #endregion

        #region NT status codes - returned as process exit codes
        private const int STATUS_NO_MEMORY = unchecked((int)0xC0000017);
        private const int STATUS_ILLEGAL_INSTRUCTION = unchecked((int)0xC000001D);
        private const int STATUS_INVALID_IMAGE_FORMAT = unchecked((int)0xC000007B);
        private const int STATUS_DLL_NOT_FOUND = unchecked((int)0xC0000135);
        private const int STATUS_ENTRYPOINT_NOT_FOUND = unchecked((int)0xC0000139);
        private const int STATUS_DLL_INIT_FAILED = unchecked((int)0xC0000142);
        private const int STATUS_STACK_BUFFER_OVERRUN = unchecked((int)0xC0000409);
        #endregion

        #region MAME exit codes - src/emu/main.h, set in clifront.cpp
        private const int EMU_ERR_FAILED_VALIDITY = 1;
        private const int EMU_ERR_MISSING_FILES = 2;
        private const int EMU_ERR_FATALERROR = 3;
        private const int EMU_ERR_DEVICE = 4;
        private const int EMU_ERR_NO_SUCH_SYSTEM = 5;
        private const int EMU_ERR_INVALID_CONFIG = 6;
        #endregion

        /// <summary>
        /// Builds a message for an exception thrown by Process.Start.
        /// Always returns a message : an emulator that cannot even be started is always worth reporting.
        /// </summary>
        public static string FromException(Exception ex, ProcessStartInfo path)
        {
            string headline = GetExeName(path) + " could not be started.";

            Win32Exception w32 = ex as Win32Exception;
            if (w32 == null)
                return Format(headline, ex.Message, "Check emulatorlauncher.log for more details.");

            SimpleLogger.Instance.Error("[Diagnostics] Win32 error " + w32.NativeErrorCode + " : " + w32.Message);

            switch (w32.NativeErrorCode)
            {
                case ERROR_FILE_NOT_FOUND:
                case ERROR_PATH_NOT_FOUND:
                    return Format(headline,
                        "some of its files are missing.",
                        "Reinstall or update this emulator, then try again.");

                case ERROR_ACCESS_DENIED:
                    return Format(headline,
                        "Windows refused to run it. This is almost always an antivirus, Windows Defender, or Controlled Folder Access.",
                        "Add the RetroBat folder to your antivirus exclusions, then try again.");

                case ERROR_NOT_ENOUGH_MEMORY:
                    return Format(headline,
                        "there is not enough memory available.",
                        "Close the other running applications, then try again.");

                case ERROR_SHARING_VIOLATION:
                    return Format(headline,
                        "one of its files is already in use.",
                        "Make sure this emulator is not already running, then try again.");

                case ERROR_MOD_NOT_FOUND:
                    return Format(headline,
                        "a file it needs is missing.",
                        "Reinstall this emulator. If the problem remains, install the Microsoft Visual C++ redistributables.");

                case ERROR_BAD_EXE_FORMAT:
                    return Format(headline,
                        "its main file is damaged or was not fully downloaded.",
                        "Reinstall this emulator to download it again.");

                case ERROR_ELEVATION_REQUIRED:
                    return Format(headline,
                        "it requires administrator rights.",
                        "Close RetroBat, right-click retrobat.exe, choose Run as administrator, then launch the game again.");

                case ERROR_DLL_INIT_FAILED:
                    return Format(headline,
                        "one of its components failed to load.",
                        "Update your graphics driver and install the Microsoft Visual C++ redistributables, then try again.");

                case ERROR_CANCELLED:
                    return Format(headline,
                        "the Windows administrator prompt was cancelled.",
                        "Launch the game again and accept the prompt.");
            }

            return Format(headline,
                w32.Message,
                "Check emulatorlauncher.log for more details.");
        }

        /// <summary>
        /// Builds a message for a process exit code that is a known Windows failure status.
        /// Returns null when the exit code is not one of them : the caller then keeps its current behaviour.
        /// </summary>
        public static string FromExitCode(int exitCode, ProcessStartInfo path)
        {
            string headline = GetExeName(path) + " stopped unexpectedly.";

            switch (exitCode)
            {
                case STATUS_NO_MEMORY:
                    return Format(headline,
                        "it ran out of memory.",
                        "Close the other running applications, then try again.");

                case STATUS_ILLEGAL_INSTRUCTION:
                    return Format(headline,
                        "it needs a more recent processor than the one in this computer.",
                        "Use another emulator for this system.");

                case STATUS_INVALID_IMAGE_FORMAT:
                    return Format(headline,
                        "it mixes 32-bit and 64-bit files, or one of its files is damaged.",
                        "Reinstall this emulator.");

                case STATUS_DLL_NOT_FOUND:
                case STATUS_ENTRYPOINT_NOT_FOUND:
                    return Format(headline,
                        "a Windows component it needs is missing.",
                        "Install the Microsoft Visual C++ redistributables and the latest DirectX runtime, then try again.");

                case STATUS_DLL_INIT_FAILED:
                    return Format(headline,
                        "it could not initialize one of its components. This is usually the graphics driver.",
                        "Update your graphics driver, then try again.");

                case STATUS_STACK_BUFFER_OVERRUN:
                    return Format(headline,
                        "it crashed.",
                        "Report the problem with the name of the game and the emulator version.");
            }

            return null;
        }

        /// <summary>
        /// Builds a message from a MAME exit code. MAME reports why it stopped through its exit code,
        /// which is far more reliable than its error.log : that file is only created once the machine is
        /// already running, so a failure to launch never reaches it.
        /// Returns null when the exit code carries no usable information.
        /// </summary>
        public static string FromMameExitCode(int exitCode, ProcessStartInfo path)
        {
            string exe = GetExeName(path);

            switch (exitCode)
            {
                case EMU_ERR_FAILED_VALIDITY:
                    return Format(exe + " stopped unexpectedly.",
                        "its internal validity checks failed.",
                        "Reinstall this emulator.");

                case EMU_ERR_MISSING_FILES:
                    return Format(exe + " could not start this game.",
                        "some ROM files are missing, or the romset does not match this version of the emulator.",
                        "Check that your romset matches the emulator version installed by RetroBat.");

                case EMU_ERR_FATALERROR:
                    return Format(exe + " stopped on a fatal error.",
                        "the emulator could not continue.",
                        "Report the problem with the name of the game.");

                case EMU_ERR_DEVICE:
                    return Format(exe + " could not start this game.",
                        "one of the machine devices could not be initialized. This is usually a missing BIOS or CHD file.",
                        "Check the BIOS and CHD files required by this game.");

                case EMU_ERR_NO_SUCH_SYSTEM:
                    return Format(exe + " could not start this game.",
                        "this version of the emulator does not know this game.",
                        "Check the rom file name, or update the emulator.");

                case EMU_ERR_INVALID_CONFIG:
                    return Format(exe + " refused the options written by RetroBat.",
                        "one of the generated settings is not valid for this emulator version.",
                        "Report this with the name of the game : this is a RetroBat issue, not a problem with your files.");
            }

            return null;
        }

        /// <summary>
        /// Builds a message with the 3-line layout shared by every launch error.
        /// Public so that generators building their own message stay consistent with this one.
        /// </summary>
        public static string Format(string headline, string cause, string action)
        {
            return headline + "\r\n" +
                   "Cause: " + cause + "\r\n" +
                   "What to do: " + action;
        }

        private static string GetExeName(ProcessStartInfo path)
        {
            try
            {
                if (path != null && !string.IsNullOrEmpty(path.FileName))
                {
                    string name = Path.GetFileNameWithoutExtension(path.FileName);
                    if (!string.IsNullOrEmpty(name))
                        return char.ToUpperInvariant(name[0]) + name.Substring(1);
                }
            }
            catch { }

            return "The emulator";
        }
    }
}
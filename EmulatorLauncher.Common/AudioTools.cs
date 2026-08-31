using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace EmulatorLauncher.Common.Audio
{
    /// <summary>
    /// Minimal MMDevice (WASAPI) interop : tells whether Windows currently exposes
    /// a usable audio render endpoint, before starting an emulator.
    /// </summary>
    public static class AudioTools
    {
        private const int eRender = 0;
        private const int eConsole = 0;
        private const int DEVICE_STATE_ACTIVE = 0x00000001;

        #region COM interop

        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        private class MMDeviceEnumeratorComObject { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int Item(int index, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, out IntPtr iface);
            [PreserveSig] int OpenPropertyStore(int stgmAccess, out IntPtr properties);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out int state);
        }

        #endregion

        /// <summary>
        /// Number of render endpoints in DEVICE_STATE_ACTIVE.
        /// Returns -1 when COM is unusable.
        /// Used by LibRetro.Generator to decide whether pinning audio_device makes sense.
        /// </summary>
        public static int GetActiveRenderEndpointCount()
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDeviceCollection collection = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                if (enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out collection) != 0 || collection == null)
                    return -1;

                int count;
                if (collection.GetCount(out count) != 0)
                    return -1;

                return count;
            }
            catch { return -1; }
            finally
            {
                if (collection != null) Marshal.ReleaseComObject(collection);
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }

        /// <summary>
        /// Waits until Windows exposes at least one active render endpoint AND a default one.
        /// Returns true when available, false on timeout. waitedMs reports the actual wait.
        /// Never blocks when COM is unusable.
        /// </summary>
        public static bool WaitForRenderEndpoint(int timeoutMs, out int waitedMs)
        {
            const int pollMs = 200;

            waitedMs = 0;
            IMMDeviceEnumerator enumerator = null;

            try
            {
                enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();

                while (true)
                {
                    IMMDeviceCollection collection = null;
                    IMMDevice device = null;

                    try
                    {
                        int count = 0;

                        if (enumerator.EnumAudioEndpoints(eRender, DEVICE_STATE_ACTIVE, out collection) == 0
                            && collection != null
                            && collection.GetCount(out count) == 0
                            && count > 0
                            && enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out device) == 0
                            && device != null)
                            return true;
                    }
                    finally
                    {
                        if (device != null) Marshal.ReleaseComObject(device);
                        if (collection != null) Marshal.ReleaseComObject(collection);
                    }

                    if (waitedMs >= timeoutMs)
                        return false;

                    Thread.Sleep(pollMs);
                    waitedMs += pollMs;
                }
            }
            catch { return true; }   // COM unusable : never block startup
            finally
            {
                if (enumerator != null) Marshal.ReleaseComObject(enumerator);
            }
        }
    }
}
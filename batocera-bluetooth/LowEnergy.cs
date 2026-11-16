using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;
using Windows.Foundation;

namespace batocera_bluetooth
{
    // The public surface has no WinRT types, so classic Bluetooth can work
    // on Windows versions where the Windows Runtime is not available.
    internal interface IBluetoothLeScan : IDisposable
    {
        bool TryPairNext();
    }

    internal static class LowEnergy
    {
        private const string Address = "System.Devices.Aep.DeviceAddress";
        private const string Appearance = "System.Devices.Aep.Bluetooth.Le.Appearance";
        private static readonly string[] Fields = { Address, Appearance };

        internal static bool Supported
        {
            get
            {
                Version version = Environment.OSVersion.Version;
                return Environment.OSVersion.Platform == PlatformID.Win32NT &&
                    (version.Major > 6 || (version.Major == 6 && version.Minor >= 2));
            }
        }

        private static string Mac(DeviceInformation device)
        {
            object value;
            if (device.Properties.TryGetValue(Address, out value) && value != null)
                return value.ToString().Replace('-', ':').ToUpperInvariant();

            Match match = Regex.Match(device.Id,
                @"([0-9A-F]{2}:){5}[0-9A-F]{2}$", RegexOptions.IgnoreCase);
            return match.Value.ToUpperInvariant();
        }

        private static string CachePath(string address)
        {
            return Path.Combine(Path.GetTempPath(),
                "RetroBat-LE-" + address.Replace(":", "").ToUpperInvariant() + ".id");
        }

        private static void PruneCache()
        {
            try
            {
                DateTime cutoff = DateTime.UtcNow.AddHours(-24);
                foreach (string path in Directory.GetFiles(Path.GetTempPath(), "RetroBat-LE-*.id"))
                {
                    if (File.GetLastWriteTimeUtc(path) < cutoff)
                        File.Delete(path);
                }
            }
            catch
            {
                // Cache cleanup must not prevent Bluetooth discovery.
            }
        }

        private static void SaveCache(DeviceInformation device)
        {
            try
            {
                File.WriteAllText(CachePath(Mac(device)), device.Id);
            }
            catch
            {
                // Discovery still works even if the temporary cache cannot be saved.
            }
        }

        private static void DeleteCache(string address)
        {
            try
            {
                File.Delete(CachePath(address));
            }
            catch
            {
                // A locked or deleted cache entry is harmless.
            }
        }

        private static bool IsPad(DeviceInformation device)
        {
            object value;
            int appearance;
            if (device.Properties.TryGetValue(Appearance, out value) &&
                int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out appearance) &&
                (appearance == 963 || appearance == 964))
                return true;

            return Regex.IsMatch(device.Name ?? String.Empty,
                @"xbox|gamepad|wireless controller|dualsense|dualshock|8bitdo|pro controller",
                RegexOptions.IgnoreCase);
        }

        private static T Wait<T>(IAsyncOperation<T> operation)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (operation.Status == AsyncStatus.Started)
            {
                if (timer.ElapsedMilliseconds > 45000)
                {
                    operation.Cancel();
                    throw new TimeoutException();
                }

                Thread.Sleep(50);
            }

            return operation.GetResults();
        }

        private static string DeviceLine(DeviceInformation device, string state)
        {
            return "<device id=\"" + Mac(device) + "\" name=\"" +
                SecurityElement.Escape(device.Name ?? String.Empty) +
                "\" type=\"joystick\"" +
                (state == null ? String.Empty : " status=\"" + state + "\"") + "/>";
        }

        private static DeviceInformation[] Paired()
        {
            return Wait(DeviceInformation.FindAllAsync(
                BluetoothLEDevice.GetDeviceSelectorFromPairingState(true),
                Fields, DeviceInformationKind.AssociationEndpoint)).Where(IsPad).ToArray();
        }

        internal static void List()
        {
            if (!Supported)
                return;

            PruneCache();
            foreach (DeviceInformation device in Paired())
            {
                SaveCache(device);
                Console.WriteLine(DeviceLine(device, null));
            }
        }

        // Return immediately for classic controllers; do not do any WinRT
        // enumeration unless previous discovery identified the address as BLE.
        internal static bool Handle(string command, string address)
        {
            if (!Supported || String.IsNullOrEmpty(address))
                return false;

            ulong number;
            if (!ulong.TryParse(address.Replace(":", ""), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out number))
                return false;

            // A classic address normally has no BLE cache. Avoid even enumerating
            // the temp directory on this fast path.
            string cache = CachePath(address);
            if (!File.Exists(cache))
                return false;

            PruneCache();
            if (!File.Exists(cache))
                return false;

            DeviceInformation device = null;
            try
            {
                string id = File.ReadAllText(cache);
                device = Wait(DeviceInformation.CreateFromIdAsync(id,
                    Fields, DeviceInformationKind.AssociationEndpoint));
            }
            catch
            {
                // A stale DeviceInformation ID should fall back to classic Bluetooth.
            }

            if (device == null || !String.Equals(Mac(device), address,
                StringComparison.OrdinalIgnoreCase))
            {
                DeleteCache(address);
                return false;
            }

            if (command == "remove")
            {
                if (device.Pairing.IsPaired &&
                    Wait(device.Pairing.UnpairAsync()).Status != DeviceUnpairingResultStatus.Unpaired)
                    Environment.ExitCode = 1;

                DeleteCache(address);
            }
            else if (command == "trust" || command == "connect")
            {
                if (!device.Pairing.IsPaired && !Pair(device))
                    Environment.ExitCode = 1;
            }

            return true;
        }

        internal static void Forget()
        {
            if (!Supported)
                return;

            PruneCache();
            foreach (DeviceInformation device in Paired())
            {
                try
                {
                    if (device.Pairing.IsPaired &&
                        Wait(device.Pairing.UnpairAsync()).Status != DeviceUnpairingResultStatus.Unpaired)
                        Environment.ExitCode = 1;
                }
                catch
                {
                    Environment.ExitCode = 1;
                }
                finally
                {
                    DeleteCache(Mac(device));
                }
            }
        }

        private static bool Pair(DeviceInformation device)
        {
            DeviceInformationCustomPairing custom = device.Pairing.Custom;
            TypedEventHandler<DeviceInformationCustomPairing, DevicePairingRequestedEventArgs> confirm =
                (sender, args) =>
                {
                    if (args.PairingKind == DevicePairingKinds.ConfirmOnly)
                        args.Accept();
                };

            custom.PairingRequested += confirm;
            try
            {
                DevicePairingResultStatus status =
                    Wait(custom.PairAsync(DevicePairingKinds.ConfirmOnly)).Status;
                return status == DevicePairingResultStatus.Paired ||
                    status == DevicePairingResultStatus.AlreadyPaired;
            }
            finally
            {
                custom.PairingRequested -= confirm;
            }
        }

        internal static IBluetoothLeScan Watch(bool automatic)
        {
            if (!Supported)
                return null;

            PruneCache();
            var scan = new Scan(automatic);
            scan.Start();
            return scan;
        }

        private sealed class Scan : IBluetoothLeScan
        {
            private readonly bool automatic;
            private readonly DeviceWatcher watcher;
            private readonly object sync = new object();
            private readonly Dictionary<string, DeviceInformation> seen =
                new Dictionary<string, DeviceInformation>();
            private readonly HashSet<string> shown = new HashSet<string>();
            private readonly Queue<string> pending = new Queue<string>();
            private bool disposed;

            internal Scan(bool automatic)
            {
                this.automatic = automatic;
                watcher = DeviceInformation.CreateWatcher(
                    BluetoothLEDevice.GetDeviceSelectorFromPairingState(false),
                    Fields, DeviceInformationKind.AssociationEndpoint);
                watcher.Added += OnAdded;
                watcher.Updated += OnUpdated;
                watcher.Removed += OnRemoved;
            }

            internal void Start()
            {
                watcher.Start();
            }

            private void Publish(DeviceInformation device)
            {
                string mac = Mac(device);
                if (disposed || String.IsNullOrEmpty(mac) ||
                    String.IsNullOrWhiteSpace(device.Name) || device.Pairing.IsPaired ||
                    !device.Pairing.CanPair || !IsPad(device) || !shown.Add(device.Id))
                    return;

                SaveCache(device);

                if (automatic)
                    pending.Enqueue(device.Id);
                else
                    Console.WriteLine(DeviceLine(device, "added"));
            }

            private void OnAdded(DeviceWatcher sender, DeviceInformation device)
            {
                try
                {
                    lock (sync)
                    {
                        if (disposed)
                            return;

                        seen[device.Id] = device;
                        Publish(device);
                    }
                }
                catch
                {
                    // Never print to stderr: EmulationStation parses all output as devices.
                }
            }

            private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
            {
                try
                {
                    lock (sync)
                    {
                        DeviceInformation device;
                        if (!disposed && seen.TryGetValue(update.Id, out device))
                        {
                            device.Update(update);
                            Publish(device);
                        }
                    }
                }
                catch
                {
                }
            }

            private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
            {
                try
                {
                    lock (sync)
                    {
                        DeviceInformation device;
                        if (disposed || !seen.TryGetValue(update.Id, out device))
                            return;

                        if (shown.Remove(update.Id))
                        {
                            if (!automatic)
                                Console.WriteLine(DeviceLine(device, "removed"));
                            DeleteCache(Mac(device));
                        }

                        seen.Remove(update.Id);
                    }
                }
                catch
                {
                }
            }

            // The main thread runs all pairing operations, including classic BT.
            // No worker can terminate the process during a classic pairing request.
            public bool TryPairNext()
            {
                if (!automatic)
                    return false;

                DeviceInformation device = null;
                lock (sync)
                {
                    while (!disposed && pending.Count > 0)
                    {
                        string id = pending.Dequeue();
                        if (seen.TryGetValue(id, out device))
                            break;
                    }
                }

                if (device == null)
                    return false;

                try
                {
                    if (Pair(device))
                    {
                        Console.WriteLine("Found \"" + device.Name + ", " + Mac(device) + "\"");
                        return true;
                    }
                }
                catch
                {
                }

                lock (sync)
                {
                    shown.Remove(device.Id);
                }

                return false;
            }

            public void Dispose()
            {
                lock (sync)
                {
                    disposed = true;
                    pending.Clear();
                }

                try
                {
                    watcher.Stop();
                }
                catch
                {
                }

                watcher.Added -= OnAdded;
                watcher.Updated -= OnUpdated;
                watcher.Removed -= OnRemoved;
            }
        }
    }
}

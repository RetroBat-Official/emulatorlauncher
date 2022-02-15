using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using emulatorLauncher.imapi2;
using System.Runtime.InteropServices.ComTypes;
using System.Management;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace emulatorLauncher.Tools
{
    class IsoFile : IDisposable
    {
        public static IsoFile MountIso(string isoFile)
        {
            IntPtr handle = IntPtr.Zero;

            var driveLetters = System.IO.DriveInfo.GetDrives();

            // open disk handle
            var openParameters = new OPEN_VIRTUAL_DISK_PARAMETERS();
            openParameters.Version = OPEN_VIRTUAL_DISK_VERSION.OPEN_VIRTUAL_DISK_VERSION_1;
            openParameters.Version1.RWDepth = OPEN_VIRTUAL_DISK_RW_DEPTH_DEFAULT;

            var openStorageType = new VIRTUAL_STORAGE_TYPE();
            openStorageType.DeviceId = VIRTUAL_STORAGE_TYPE_DEVICE_ISO;
            openStorageType.VendorId = VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT;

            int openResult = OpenVirtualDisk(ref openStorageType, isoFile,
                VIRTUAL_DISK_ACCESS_MASK.VIRTUAL_DISK_ACCESS_ATTACH_RO,
                OPEN_VIRTUAL_DISK_FLAG.OPEN_VIRTUAL_DISK_FLAG_NONE,
                ref openParameters, ref handle);

            if (openResult != ERROR_SUCCESS)
                return null;

            // attach disk - permanently
            var attachParameters = new ATTACH_VIRTUAL_DISK_PARAMETERS();
            attachParameters.Version = ATTACH_VIRTUAL_DISK_VERSION.ATTACH_VIRTUAL_DISK_VERSION_1;
            int attachResult = AttachVirtualDisk(handle, IntPtr.Zero, ATTACH_VIRTUAL_DISK_FLAG.ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY, 0, ref attachParameters, IntPtr.Zero);
            if (attachResult != ERROR_SUCCESS)
            {
                CloseHandle(handle);
                return null;
            }

            var newDriveLetters = System.IO.DriveInfo.GetDrives();

            var drive = newDriveLetters.Where(d => d.DriveType == DriveType.CDRom && !driveLetters.Any(l => l.Name == d.Name)).FirstOrDefault();
            if (drive == null)
            {
                CloseHandle(handle);
                return null;
            }

            return new IsoFile(handle, isoFile, drive);
        }

        private IsoFile(IntPtr handle, string iso, DriveInfo drive)
        {
            Handle = handle;
            Iso = iso;
            Drive = drive;
        }

        public IntPtr Handle { get; set; }
        public string Iso { get; set; }
        public DriveInfo Drive { get; set; }

        public void UnMount()
        {
            if (Handle != IntPtr.Zero)
            {
                CloseHandle(Handle);
                Handle = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            UnMount();
        }

        #region Virtual Disk Apis
        public const Int32 ERROR_SUCCESS = 0;

        public const int OPEN_VIRTUAL_DISK_RW_DEPTH_DEFAULT = 1;


        public const int VIRTUAL_STORAGE_TYPE_DEVICE_ISO = 1;
        public const int VIRTUAL_STORAGE_TYPE_DEVICE_VHD = 2;

        public static readonly Guid VIRTUAL_STORAGE_TYPE_VENDOR_MICROSOFT = new Guid("EC984AEC-A0F9-47e9-901F-71415A66345B");


        public enum ATTACH_VIRTUAL_DISK_FLAG : int
        {
            ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
            ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001,
            ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002,
            ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004,
            ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008
        }

        public enum ATTACH_VIRTUAL_DISK_VERSION : int
        {
            ATTACH_VIRTUAL_DISK_VERSION_UNSPECIFIED = 0,
            ATTACH_VIRTUAL_DISK_VERSION_1 = 1
        }

        public enum OPEN_VIRTUAL_DISK_FLAG : int
        {
            OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000,
            OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001,
            OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002,
            OPEN_VIRTUAL_DISK_FLAG_BOOT_DRIVE = 0x00000004
        }

        public enum OPEN_VIRTUAL_DISK_VERSION : int
        {
            OPEN_VIRTUAL_DISK_VERSION_1 = 1
        }


        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ATTACH_VIRTUAL_DISK_PARAMETERS
        {
            public ATTACH_VIRTUAL_DISK_VERSION Version;
            public ATTACH_VIRTUAL_DISK_PARAMETERS_Version1 Version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct ATTACH_VIRTUAL_DISK_PARAMETERS_Version1
        {
            public Int32 Reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS
        {
            public OPEN_VIRTUAL_DISK_VERSION Version;
            public OPEN_VIRTUAL_DISK_PARAMETERS_Version1 Version1;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct OPEN_VIRTUAL_DISK_PARAMETERS_Version1
        {
            public Int32 RWDepth;
        }

        public enum VIRTUAL_DISK_ACCESS_MASK : int
        {
            VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000,
            VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000,
            VIRTUAL_DISK_ACCESS_DETACH = 0x00040000,
            VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000,
            VIRTUAL_DISK_ACCESS_CREATE = 0x00100000,
            VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000,
            VIRTUAL_DISK_ACCESS_READ = 0x000d0000,
            VIRTUAL_DISK_ACCESS_ALL = 0x003f0000,
            VIRTUAL_DISK_ACCESS_WRITABLE = 0x00320000
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct VIRTUAL_STORAGE_TYPE
        {
            public Int32 DeviceId;
            public Guid VendorId;
        }


        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 AttachVirtualDisk(IntPtr VirtualDiskHandle, IntPtr SecurityDescriptor, ATTACH_VIRTUAL_DISK_FLAG Flags, Int32 ProviderSpecificFlags, ref ATTACH_VIRTUAL_DISK_PARAMETERS Parameters, IntPtr Overlapped);

        [DllImportAttribute("kernel32.dll", SetLastError = true)]
        [return: MarshalAsAttribute(UnmanagedType.Bool)]
        public static extern Boolean CloseHandle(IntPtr hObject);

        [DllImport("virtdisk.dll", CharSet = CharSet.Unicode)]
        public static extern Int32 OpenVirtualDisk(ref VIRTUAL_STORAGE_TYPE VirtualStorageType, String Path, VIRTUAL_DISK_ACCESS_MASK VirtualDiskAccessMask, OPEN_VIRTUAL_DISK_FLAG Flags, ref OPEN_VIRTUAL_DISK_PARAMETERS Parameters, ref IntPtr Handle);
        #endregion

        private static void ReleaseIFsiItems(IFsiDirectoryItem rootItem)
        {
            if (rootItem == null)
            {
                return;
            }

            var enm = rootItem.GetEnumerator();
            while (enm.MoveNext())
            {
                var currentItem = enm.Current as IFsiItem;
                var fsiFileItem = currentItem as IFsiFileItem;
                if (fsiFileItem != null)
                {
                    try
                    {
                        var stream = fsiFileItem.Data;
                        var iUnknownForObject = Marshal.GetIUnknownForObject(stream);
                        // Get a reference - things go badly wrong if we release a 0 ref count stream!
                        var i = Marshal.AddRef(iUnknownForObject);
                        // Release all references
                        while (i > 0)
                        {
                            i = Marshal.Release(iUnknownForObject);
                        }
                        Marshal.FinalReleaseComObject(stream);
                    }
                    catch (COMException)
                    {
                        // Thrown when accessing fsiFileItem.Data
                    }
                }
                else
                {
                    ReleaseIFsiItems(currentItem as IFsiDirectoryItem);
                }
            }
        }

        public static void MakeIso(string folder, string isoFile)
        {
            var iso = new MsftFileSystemImage();
            iso.ChooseImageDefaultsForMediaType(IMAPI_MEDIA_PHYSICAL_TYPE.IMAPI_MEDIA_TYPE_DISK);
            iso.FileSystemsToCreate = FsiFileSystems.FsiFileSystemISO9660 | FsiFileSystems.FsiFileSystemJoliet;
            iso.Root.AddTree(folder, false);         

            IFileSystemImageResult resultImage = iso.CreateResultImage();
            IStream imageStream = resultImage.ImageStream;
            IStream newStream = null;
                        
            if (imageStream != null)
            {
                System.Runtime.InteropServices.ComTypes.STATSTG stat;
                imageStream.Stat(out stat, 0x1);
                
                int res = SHCreateStreamOnFile(isoFile, STGM_CREATE | STGM_WRITE, out newStream);
                if (res == 0 && newStream != null)
                {
                    IntPtr inBytes = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(long)));
                    IntPtr outBytes = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(long)));

                    try
                    {
                        imageStream.CopyTo(newStream, stat.cbSize, inBytes, outBytes);
                    }
                    finally
                    {                        
                        Marshal.FinalReleaseComObject(imageStream);
                        newStream.Commit(0);                        
                        Marshal.FinalReleaseComObject(newStream);
                        Marshal.FreeHGlobal(inBytes);
                        Marshal.FreeHGlobal(outBytes);
                        Marshal.FinalReleaseComObject(resultImage);

                        ReleaseIFsiItems(iso.Root);
                        Marshal.FinalReleaseComObject(iso);
                    }
                }
                else
                {
                    Marshal.FinalReleaseComObject(imageStream);
                    Marshal.FinalReleaseComObject(resultImage);

                    ReleaseIFsiItems(iso.Root);
                    Marshal.FinalReleaseComObject(iso);
                }
            }
            else
            {
                Marshal.FinalReleaseComObject(resultImage);

                ReleaseIFsiItems(iso.Root);
                Marshal.FinalReleaseComObject(iso);
            }
        }

        #region Make iso Apis
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        static internal extern int SHCreateStreamOnFile(string pszFile, uint grfMode, out IStream ppstm);

        internal const uint STGM_WRITE = 0x00000001;
        internal const uint STGM_CREATE = 0x00001000;
        #endregion

        public static void ConvertToIso(string source)
        {
            if (!Zip.IsCompressedFile(source))
                return;

            string isoFile = Path.ChangeExtension(source, ".iso");
            
            if (File.Exists(isoFile))
                File.Delete(isoFile);

            if (File.Exists(isoFile))
                return;

            using (var progress = new ProgressInformation("Extraction..."))
            {
                string extractionPath = Path.ChangeExtension(source, ".extraction");
              
                try { Directory.Delete(extractionPath, true); }
                catch { }

                Zip.Extract(source, extractionPath);

                Zip.CleanupUncompressedWSquashFS(source, extractionPath);
                
                progress.SetText("Compression...");

                System.Windows.Forms.Application.DoEvents();

                MakeIso(extractionPath, isoFile);

                try { Directory.Delete(extractionPath, true); }
                catch 
                { 
                }
            }
        }
    }
}

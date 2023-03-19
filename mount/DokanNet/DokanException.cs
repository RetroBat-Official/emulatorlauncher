using System;
using DokanNet.Properties;

namespace DokanNet
{
    /// <summary>
    /// The dokan exception.
    /// </summary>
    [Serializable]
    public class DokanException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DokanException"/> class with a <see cref="Exception.HResult"/>.
        /// </summary>
        /// <param name="status">
        /// The error status also written to <see cref="Exception.HResult"/>.
        /// </param>
        internal DokanException(DokanStatus status)
            : this(status, GetStatusErrorMessage(status)) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DokanException"/> class with a <see cref="Exception.HResult"/>.
        /// </summary>
        /// <param name="status">
        /// The error status also written to <see cref="Exception.HResult"/>.
        /// </param>
        /// <param name="message">
        /// The error message.
        /// </param>
        internal DokanException(DokanStatus status, string message)
            : base(message)
        {
            ErrorStatus = status;
            HResult = (int)status;
        }

        private static string GetStatusErrorMessage(DokanStatus status)
        {
            switch (status)
            {
                case DokanStatus.Error:
                    return "Dokan error	";
                case DokanStatus.DriveLetterError:
                    return "Bad drive letter";
                case DokanStatus.DriverInstallError:
                    return "Can't install the Dokan driver";
                case DokanStatus.MountError:
                    return "Can't assign a drive letter or mount point";
                case DokanStatus.StartError:
                    return "Something's wrong with the Dokan driver";
                case DokanStatus.MountPointError:
                    return "Mount point is invalid";
                case DokanStatus.VersionError:
                    return "Version error";
                default:
                    return "Unknown error";
            }
        }

        /// <summary>
        /// Dokan error status <see cref="DokanStatus"/>.
        /// </summary>
        public DokanStatus ErrorStatus { get; private set; }
    }
}
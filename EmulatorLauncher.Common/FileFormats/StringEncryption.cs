using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace EmulatorLauncher.Common.FileFormats
{
    public static class EncryptStrings
    {
        private static readonly Aes _aes;

        static EncryptStrings()
        {
            _aes = Aes.Create();
            var machineIdBytes = Encoding.UTF8.GetBytes($"{GetMachineGuid()}/{Environment.MachineName}");
            var machineIdHash = ComputeSHA256(machineIdBytes);
            _aes.Key = machineIdHash;

            // Combine the hash and original bytes for IV calculation
            var ivSource = new byte[machineIdHash.Length + machineIdBytes.Length];
            Array.Copy(machineIdHash, 0, ivSource, 0, machineIdHash.Length);
            Array.Copy(machineIdBytes, 0, ivSource, machineIdHash.Length, machineIdBytes.Length);

            _aes.IV = ComputeSHA256(ivSource).Take(16).ToArray(); // Take first 16 bytes
            _aes.Mode = CipherMode.CBC;
            _aes.Padding = PaddingMode.PKCS7;
        }

        private static string GetMachineGuid()
        {
            try
            {
                RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                if (baseKey != null)
                {
                    using (RegistryKey subKey = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                    {
                        if (subKey != null)
                        {
                            object machineGuid = subKey.GetValue("MachineGuid");
                            return machineGuid != null ? machineGuid.ToString() : string.Empty;
                        }
                    }
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty; // Fallback in case of an error
            }
        }

        private static byte[] ComputeSHA256(byte[] input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                return sha256.ComputeHash(input);
            }
        }

        public static string EncryptString(string secretString)
        {
            var bytes = Encoding.UTF8.GetBytes(secretString);

            using (var ms = new MemoryStream())
            {
                using (var encryptor = _aes.CreateEncryptor())
                {
                    using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(bytes, 0, bytes.Length);
                        cryptoStream.FlushFinalBlock(); // Ensures all data is written to the stream
                    }
                }

                return Convert.ToBase64String(ms.ToArray());
            }
        }

        public static string DecryptString(string encryptedString)
        {
            try
            {
                if (string.IsNullOrEmpty(encryptedString))
                {
                    return string.Empty;
                }

                var bytes = Convert.FromBase64String(encryptedString);

                using (var ms = new MemoryStream())
                {
                    using (var decryptor = _aes.CreateDecryptor())
                    {
                        using (var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                        {
                            cryptoStream.Write(bytes, 0, bytes.Length);
                            cryptoStream.FlushFinalBlock(); // Ensure all data is processed
                        }
                    }

                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
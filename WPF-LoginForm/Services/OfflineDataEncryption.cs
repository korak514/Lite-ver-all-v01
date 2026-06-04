using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WPF_LoginForm.Services
{
    public static class OfflineDataEncryption
    {
        // Portable master password for offline data encryption — same on every PC
        public const string MasterPassword = "Biosun@4Jp2!";

        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int KeyLength = 16;
        private const int HmacLength = 32;
        private const int Iterations = 10000;

        public static byte[] GenerateSalt()
        {
            byte[] salt = new byte[SaltLength];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);
            return salt;
        }

        public static byte[] GenerateIV()
        {
            byte[] iv = new byte[IvLength];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(iv);
            return iv;
        }

        public static byte[] DeriveKey(string password, byte[] salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256))
                return pbkdf2.GetBytes(KeyLength);
        }

        public static byte[] Encrypt(byte[] plaintext, string password)
        {
            byte[] salt = GenerateSalt();
            byte[] iv = GenerateIV();
            byte[] key = DeriveKey(password, salt);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    ms.Write(salt, 0, salt.Length);
                    ms.Write(iv, 0, iv.Length);

                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plaintext, 0, plaintext.Length);
                        cs.FlushFinalBlock();
                    }

                    byte[] ciphertext = ms.ToArray();

                    // Append HMAC-SHA256 for integrity verification
                    byte[] hmac = ComputeHmac(ciphertext, key);
                    using (var result = new MemoryStream())
                    {
                        result.Write(ciphertext, 0, ciphertext.Length);
                        result.Write(hmac, 0, hmac.Length);
                        return result.ToArray();
                    }
                }
            }
        }

        public static byte[] Decrypt(byte[] ciphertextWithHmac, string password)
        {
            if (ciphertextWithHmac == null || ciphertextWithHmac.Length < SaltLength + IvLength)
                throw new ArgumentException("Invalid ciphertext");

            // Try new format (with HMAC footer) first
            if (ciphertextWithHmac.Length >= SaltLength + IvLength + HmacLength)
            {
                try
                {
                    return DecryptWithHmac(ciphertextWithHmac, password);
                }
                catch (InvalidOperationException)
                {
                    // HMAC mismatch — fall through to legacy format
                }
            }

            // Legacy format (no HMAC)
            return DecryptLegacy(ciphertextWithHmac, password);
        }

        private static byte[] DecryptWithHmac(byte[] ciphertextWithHmac, string password)
        {
            byte[] ciphertext = new byte[ciphertextWithHmac.Length - HmacLength];
            byte[] storedHmac = new byte[HmacLength];
            Buffer.BlockCopy(ciphertextWithHmac, 0, ciphertext, 0, ciphertext.Length);
            Buffer.BlockCopy(ciphertextWithHmac, ciphertext.Length, storedHmac, 0, HmacLength);

            byte[] salt = new byte[SaltLength];
            Buffer.BlockCopy(ciphertext, 0, salt, 0, SaltLength);
            byte[] key = DeriveKey(password, salt);

            byte[] computedHmac = ComputeHmac(ciphertext, key);
            if (!ConstantTimeEquals(storedHmac, computedHmac))
                throw new InvalidOperationException("File integrity check failed");

            return DecryptCore(ciphertext, key);
        }

        private static byte[] DecryptLegacy(byte[] ciphertext, string password)
        {
            byte[] salt = new byte[SaltLength];
            byte[] iv = new byte[IvLength];
            Buffer.BlockCopy(ciphertext, 0, salt, 0, SaltLength);
            Buffer.BlockCopy(ciphertext, SaltLength, iv, 0, IvLength);

            byte[] key = DeriveKey(password, salt);
            return DecryptCore(ciphertext, key);
        }

        private static byte[] DecryptCore(byte[] ciphertextWithSalt, byte[] key)
        {
            byte[] iv = new byte[IvLength];
            Buffer.BlockCopy(ciphertextWithSalt, SaltLength, iv, 0, IvLength);
            byte[] encryptedData = new byte[ciphertextWithSalt.Length - SaltLength - IvLength];
            Buffer.BlockCopy(ciphertextWithSalt, SaltLength + IvLength, encryptedData, 0, encryptedData.Length);

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(encryptedData))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var resultMs = new MemoryStream())
                {
                    cs.CopyTo(resultMs);
                    return resultMs.ToArray();
                }
            }
        }

        private static byte[] ComputeHmac(byte[] data, byte[] key)
        {
            using (var hmac = new HMACSHA256(key))
                return hmac.ComputeHash(data);
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++)
                diff |= a[i] ^ b[i];
            return diff == 0;
        }

        public static string ObfuscatePassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 4)
                return "****";
            return password.Substring(0, 2) + "***" + password.Substring(password.Length - 2);
        }

        public static string ObfuscateBase64(string base64)
        {
            if (string.IsNullOrEmpty(base64) || base64.Length < 10)
                return "****";
            return base64.Substring(0, 6) + "***" + base64.Substring(base64.Length - 4);
        }

    }

    public static class OfflineDataEncryptionFile
    {
        private static string LockPath(string filePath) => filePath + ".lock";

        public static void EncryptFile(string filePath, byte[] plaintext, string password)
        {
            using (FileLock.Acquire(LockPath(filePath)))
            {
                byte[] encrypted = OfflineDataEncryption.Encrypt(plaintext, password);
                File.WriteAllBytes(filePath, encrypted);
            }
        }

        public static byte[] DecryptFile(string filePath, string password)
        {
            using (FileLock.Acquire(LockPath(filePath)))
            {
                byte[] encryptedBytes = File.ReadAllBytes(filePath);
                return OfflineDataEncryption.Decrypt(encryptedBytes, password);
            }
        }
    }

    public static class OfflineDataCache
    {
        public static ConcurrentDictionary<string, DataTable> DecryptedTables { get; }
            = new ConcurrentDictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

        private static ConcurrentDictionary<string, DateTime> _fileTimestamps
            = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        public static bool IsStale(string tableName, string filePath)
        {
            if (!_fileTimestamps.TryGetValue(tableName, out DateTime cachedTime))
                return true;

            try
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(filePath);
                return lastWrite > cachedTime;
            }
            catch
            {
                return true;
            }
        }

        public static void UpdateTimestamp(string tableName, string filePath)
        {
            try
            {
                DateTime lastWrite = File.GetLastWriteTimeUtc(filePath);
                _fileTimestamps[tableName] = lastWrite;
            }
            catch
            {
                _fileTimestamps.TryRemove(tableName, out _);
            }
        }

        public static void Clear()
        {
            DecryptedTables.Clear();
            _fileTimestamps.Clear();
        }
    }

    /// <summary>
    /// Shared CSV parser that auto-detects delimiter (used by both MainViewModel and SettingsViewModel).
    /// CSV files exported by DataImportExportHelper use ';' for Turkish Excel compatibility.
    /// </summary>
    public static class CsvParser
    {
        public static char DetectDelimiter(string headerLine)
        {
            int commaCount = 0, semiCount = 0, tabCount = 0;
            for (int i = 0; i < headerLine.Length; i++)
            {
                char c = headerLine[i];
                if (c == ',') commaCount++;
                else if (c == ';') semiCount++;
                else if (c == '\t') tabCount++;
            }
            if (semiCount > commaCount && semiCount > tabCount) return ';';
            if (tabCount > commaCount && tabCount > semiCount) return '\t';
            return ',';
        }

        public static List<string> SplitCsvLine(string line, char delimiter)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var sb = new StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"') inQuotes = !inQuotes;
                else if (c == delimiter && !inQuotes)
                {
                    result.Add(sb.ToString().Trim());
                    sb.Clear();
                }
                else sb.Append(c);
            }
            result.Add(sb.ToString().Trim());
            return result;
        }

        /// <summary>
        /// Parses CSV content (with auto-detected delimiter) into a DataTable.
        /// </summary>
        public static DataTable ParseToDataTable(string csvContent, string tableName)
        {
            var dt = new DataTable(tableName);
            using (var reader = new StringReader(csvContent))
            {
                string headerLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(headerLine)) return dt;

                char delimiter = DetectDelimiter(headerLine);
                var headers = SplitCsvLine(headerLine, delimiter);

                foreach (var h in headers)
                {
                    string colName = h.Trim().Trim('"');
                    if (string.IsNullOrWhiteSpace(colName)) colName = "Column";
                    int dup = 1;
                    string orig = colName;
                    while (dt.Columns.Contains(colName))
                        colName = $"{orig}_{dup++}";
                    dt.Columns.Add(colName);
                }

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var values = SplitCsvLine(line, delimiter);
                    if (values.Count > 0)
                    {
                        var row = dt.NewRow();
                        for (int i = 0; i < Math.Min(values.Count, dt.Columns.Count); i++)
                            row[i] = values[i];
                        dt.Rows.Add(row);
                    }
                }
            }
            return dt;
        }
    }
}

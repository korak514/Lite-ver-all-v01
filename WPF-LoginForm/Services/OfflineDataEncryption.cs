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
        private const int SaltLength = 16;
        private const int IvLength = 16;
        private const int KeyLength = 16;
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

                    return ms.ToArray();
                }
            }
        }

        public static byte[] Decrypt(byte[] ciphertext, string password)
        {
            if (ciphertext == null || ciphertext.Length < SaltLength + IvLength)
                throw new ArgumentException("Invalid ciphertext");

            byte[] salt = new byte[SaltLength];
            byte[] iv = new byte[IvLength];
            Buffer.BlockCopy(ciphertext, 0, salt, 0, SaltLength);
            Buffer.BlockCopy(ciphertext, SaltLength, iv, 0, IvLength);

            byte[] key = DeriveKey(password, salt);
            byte[] encryptedData = new byte[ciphertext.Length - SaltLength - IvLength];
            Buffer.BlockCopy(ciphertext, SaltLength + IvLength, encryptedData, 0, encryptedData.Length);

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

        public static string ProtectPassword(string plaintext)
        {
            if (string.IsNullOrEmpty(plaintext)) return string.Empty;
            byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
            byte[] protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }

        public static string UnprotectPassword(string protectedBase64)
        {
            if (string.IsNullOrEmpty(protectedBase64)) return string.Empty;
            byte[] protectedBytes = Convert.FromBase64String(protectedBase64);
            byte[] plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
    }

    public static class OfflineDataCache
    {
        public static ConcurrentDictionary<string, DataTable> DecryptedTables { get; }
            = new ConcurrentDictionary<string, DataTable>(StringComparer.OrdinalIgnoreCase);

        public static void Clear()
        {
            DecryptedTables.Clear();
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

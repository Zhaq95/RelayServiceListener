using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsServiceSap.HelperClasses
{
    public static class EncryptionHelper
    {
        private static readonly string EncryptionKey = "YourDefaultKey12"; // Same key as used during encryption

        public static string Decrypt(string encryptedText)
        {
            byte[] key = Encoding.UTF8.GetBytes(EncryptionKey); // Ensure the key is of valid size (16, 24, or 32 bytes)
            byte[] cipherTextWithIv = Convert.FromBase64String(encryptedText);

            using (var aes = Aes.Create())
            {
                aes.Key = key;

                // Extract the IV from the start of the ciphertext
                byte[] iv = new byte[aes.BlockSize / 8];
                Array.Copy(cipherTextWithIv, 0, iv, 0, iv.Length);

                aes.IV = iv; // Set the extracted IV

                // Extract the actual ciphertext (after the IV)
                byte[] cipherText = new byte[cipherTextWithIv.Length - iv.Length];
                Array.Copy(cipherTextWithIv, iv.Length, cipherText, 0, cipherText.Length);

                using (var decryptor = aes.CreateDecryptor())
                using (var ms = new MemoryStream(cipherText))
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd(); // Return the decrypted text
                }
            }
        }
    }
}


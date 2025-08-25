using System;
using System.Security.Cryptography;

namespace KingdomHeartsCustomMusic.OpenKH
{
    public static class EgsEncryption
    {
        public static byte[] GenerateKey(byte[] seed, int passCount)
        {
            // Simplified key generation - in a full implementation this would be more complex
            var key = new byte[16];
            Array.Copy(seed, key, Math.Min(seed.Length, 16));
            
            // Apply some transformations based on pass count
            for (int i = 0; i < passCount; i++)
            {
                for (int j = 0; j < key.Length; j++)
                {
                    key[j] = (byte)(key[j] ^ (i + j));
                }
            }
            
            return key;
        }

        public static void DecryptChunk(byte[] key, byte[] data, int offset, int passCount)
        {
            // Simplified decryption - XOR with key
            for (int i = 0; i < Math.Min(16, data.Length - offset); i++)
            {
                if (offset + i < data.Length)
                {
                    data[offset + i] ^= key[i % key.Length];
                }
            }
        }

        public static byte[] Encrypt(byte[] data, byte[] seed)
        {
            // Simplified encryption - XOR with seed
            var encrypted = new byte[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                encrypted[i] = (byte)(data[i] ^ seed[i % seed.Length]);
            }
            return encrypted;
        }

        public static byte[] Decrypt(byte[] data, byte[] seed)
        {
            // Decryption is the same as encryption for XOR
            return Encrypt(data, seed);
        }
    }
}
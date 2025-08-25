using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace KingdomHeartsCustomMusic.OpenKH
{
    public static class Helpers
    {
        public static string ToString(byte[] data)
        {
            return BitConverter.ToString(data).Replace("-", "").ToUpperInvariant();
        }

        public static byte[] ToBytes(string hexString)
        {
            return Enumerable.Range(0, hexString.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hexString.Substring(x, 2), 16))
                             .ToArray();
        }

        public static string CreateMD5(string input)
        {
            using var md5 = MD5.Create();
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            return ToString(hashBytes);
        }

        public static byte[] GetHashData(byte[] data)
        {
            using var md5 = MD5.Create();
            return md5.ComputeHash(data);
        }

        public static byte[] CompressData(byte[] data)
        {
            using var input = new MemoryStream(data);
            using var output = new MemoryStream();
            using var deflate = new DeflateStream(output, CompressionLevel.Optimal);
            
            // Add zlib header (2 bytes)
            output.WriteByte(0x78);
            output.WriteByte(0x9C);
            
            input.CopyTo(deflate);
            deflate.Close();
            
            return output.ToArray();
        }

        public static string GetRelativePath(string fullPath, string basePath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;

            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);

            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString());
        }

        public static int IndexOfByteArray(byte[] array, byte[] pattern, int startIndex = 0)
        {
            for (int i = startIndex; i <= array.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (array[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return i;
            }
            return -1;
        }
    }
}
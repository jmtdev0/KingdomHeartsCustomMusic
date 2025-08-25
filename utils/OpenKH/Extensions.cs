using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace KingdomHeartsCustomMusic.OpenKH
{
    public static class Extensions
    {
        public static T SetPosition<T>(this T stream, long position) where T : Stream
        {
            stream.Seek(position, SeekOrigin.Begin);
            return stream;
        }

        public static T AlignPosition<T>(this T stream, int alignValue) where T : Stream
        {
            long newAlign = Align(stream.Position, alignValue);
            return stream.SetPosition(newAlign);
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            var data = new byte[length];
            stream.Read(data, 0, length);
            return data;
        }

        public static byte[] ReadAllBytes(this Stream stream)
        {
            var data = stream.SetPosition(0).ReadBytes((int)stream.Length);
            stream.Position = 0;
            return data;
        }

        public static void Write(this Stream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
        }

        public static int ReadInt32(this Stream stream)
        {
            return stream.ReadByte() | (stream.ReadByte() << 8) |
                   (stream.ReadByte() << 16) | (stream.ReadByte() << 24);
        }

        public static long Align(long offset, int alignment)
        {
            var misalignment = offset % alignment;
            return misalignment > 0 ? offset + alignment - misalignment : offset;
        }

        public static byte[] GetHashData(byte[] b)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                return md5.ComputeHash(b);
            }
        }

        public static void Using<T>(this T disposable, Action<T> action) where T : IDisposable
        {
            using (disposable)
                action(disposable);
        }

        public static TResult Using<T, TResult>(this T disposable, Func<T, TResult> func) where T : IDisposable
        {
            using (disposable)
                return func(disposable);
        }
    }
}
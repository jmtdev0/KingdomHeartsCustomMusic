using System;
using System.IO;
using System.Reflection;

namespace Xe.BinaryMapper
{
    [AttributeUsage(AttributeTargets.Property)]
    public class DataAttribute : Attribute
    {
        public int Count { get; set; } = -1;
    }

    public static class BinaryMapping
    {
        public static T ReadObject<T>(Stream stream, int baseOffset = 0) where T : class
        {
            var obj = Activator.CreateInstance<T>();
            return ReadObject(stream, obj, baseOffset);
        }

        public static T ReadObject<T>(Stream stream, T item, int baseOffset = 0) where T : class
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            foreach (var prop in properties)
            {
                var dataAttr = prop.GetCustomAttribute<DataAttribute>();
                if (dataAttr == null) continue;

                if (prop.PropertyType == typeof(int))
                {
                    var bytes = new byte[4];
                    stream.Read(bytes, 0, 4);
                    prop.SetValue(item, BitConverter.ToInt32(bytes, 0));
                }
                else if (prop.PropertyType == typeof(string))
                {
                    var length = dataAttr.Count > 0 ? dataAttr.Count : 32;
                    var bytes = new byte[length];
                    stream.Read(bytes, 0, length);
                    var str = System.Text.Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    prop.SetValue(item, str);
                }
            }

            return item;
        }

        public static T WriteObject<T>(Stream stream, T item, int baseOffset = 0) where T : class
        {
            var type = typeof(T);
            var properties = type.GetProperties();

            foreach (var prop in properties)
            {
                var dataAttr = prop.GetCustomAttribute<DataAttribute>();
                if (dataAttr == null) continue;

                if (prop.PropertyType == typeof(int))
                {
                    var value = (int)prop.GetValue(item);
                    var bytes = BitConverter.GetBytes(value);
                    stream.Write(bytes, 0, 4);
                }
                else if (prop.PropertyType == typeof(string))
                {
                    var length = dataAttr.Count > 0 ? dataAttr.Count : 32;
                    var str = (string)prop.GetValue(item) ?? "";
                    var bytes = new byte[length];
                    var strBytes = System.Text.Encoding.UTF8.GetBytes(str);
                    Array.Copy(strBytes, bytes, Math.Min(strBytes.Length, length));
                    stream.Write(bytes, 0, length);
                }
            }

            return item;
        }
    }
}
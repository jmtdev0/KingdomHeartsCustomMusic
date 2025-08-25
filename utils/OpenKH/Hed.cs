using System;
using System.Collections.Generic;
using System.IO;
using Xe.BinaryMapper;

namespace KingdomHeartsCustomMusic.OpenKH
{
    public static class Hed
    {
        public class Entry
        {
            [Data] public byte[] MD5 { get; set; } = new byte[16];
            [Data] public int ActualLength { get; set; }
            [Data] public int DataLength { get; set; }
            [Data] public long Offset { get; set; }
        }

        public static IEnumerable<Entry> Read(Stream stream)
        {
            try
            {
                stream.Position = 0;
                
                // HED files don't start with a count - they have a different structure
                // Let's read the file size and calculate entries
                long fileSize = stream.Length;
                
                // Each HED entry is 32 bytes (16 bytes MD5 + 4 bytes ActualLength + 4 bytes DataLength + 8 bytes Offset)
                int entrySize = 32;
                int entryCount = (int)(fileSize / entrySize);
                
                var entries = new List<Entry>();
                
                for (int i = 0; i < entryCount; i++)
                {
                    try
                    {
                        // Read MD5 (16 bytes)
                        var md5 = new byte[16];
                        int bytesRead = stream.Read(md5, 0, 16);
                        if (bytesRead != 16)
                        {
                            break;
                        }
                        
                        // Read ActualLength (4 bytes)
                        var actualLengthBytes = new byte[4];
                        bytesRead = stream.Read(actualLengthBytes, 0, 4);
                        if (bytesRead != 4) break;
                        int actualLength = BitConverter.ToInt32(actualLengthBytes, 0);
                        
                        // Read DataLength (4 bytes)
                        var dataLengthBytes = new byte[4];
                        bytesRead = stream.Read(dataLengthBytes, 0, 4);
                        if (bytesRead != 4) break;
                        int dataLength = BitConverter.ToInt32(dataLengthBytes, 0);
                        
                        // Read Offset (8 bytes)
                        var offsetBytes = new byte[8];
                        bytesRead = stream.Read(offsetBytes, 0, 8);
                        if (bytesRead != 8) break;
                        long offset = BitConverter.ToInt64(offsetBytes, 0);
                        
                        var entry = new Entry()
                        {
                            MD5 = md5,
                            ActualLength = actualLength,
                            DataLength = dataLength,
                            Offset = offset
                        };
                        
                        entries.Add(entry);
                    }
                    catch (Exception)
                    {
                        break;
                    }
                }
                
                return entries;
            }
            catch (Exception)
            {
                return new List<Entry>();
            }
        }

        public static void Write(Stream stream, IEnumerable<Entry> entries)
        {
            var entriesList = entries.ToList();
            
            // Write entries directly (no count header in HED files)
            foreach (var entry in entriesList)
            {
                try
                {
                    // Write MD5 (16 bytes)
                    stream.Write(entry.MD5, 0, 16);
                    
                    // Write ActualLength (4 bytes)
                    var actualLengthBytes = BitConverter.GetBytes(entry.ActualLength);
                    stream.Write(actualLengthBytes, 0, 4);
                    
                    // Write DataLength (4 bytes)
                    var dataLengthBytes = BitConverter.GetBytes(entry.DataLength);
                    stream.Write(dataLengthBytes, 0, 4);
                    
                    // Write Offset (8 bytes)
                    var offsetBytes = BitConverter.GetBytes(entry.Offset);
                    stream.Write(offsetBytes, 0, 8);
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }
    }
}
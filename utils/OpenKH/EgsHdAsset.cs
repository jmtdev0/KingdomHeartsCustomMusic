using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xe.BinaryMapper;

namespace KingdomHeartsCustomMusic.OpenKH
{
    public class EgsHdAsset
    {
        public class Header
        {
            [Data] public int DecompressedLength { get; set; }
            [Data] public int RemasteredAssetCount { get; set; }
            [Data] public int CompressedLength { get; set; }
            [Data] public int CreationDate { get; set; }
        }

        public class RemasteredEntry
        {
            [Data(Count = 0x20)] public string Name { get; set; } = "";
            [Data] public int Offset { get; set; }
            [Data] public int OriginalAssetOffset { get; set; }
            [Data] public int DecompressedLength { get; set; }
            [Data] public int CompressedLength { get; set; }
        }

        private const int PASS_COUNT = 10;

        private readonly Stream? _stream;
        private readonly Header _header;
        private readonly byte[]? _key;
        private readonly byte[] _seed;
        private readonly long _baseOffset;
        private readonly long _dataOffset;
        private readonly Dictionary<string, RemasteredEntry> _entries;
        private byte[] _originalData;
        private byte[] _originalRawData;
        private readonly Dictionary<string, byte[]> _remasteredAssetsData = new();
        private readonly Dictionary<string, byte[]> _remasteredAssetsRawData = new();

        public string[] Assets { get; }
        public byte[] Seed => _seed;
        public Header OriginalAssetHeader => _header;
        public Dictionary<string, RemasteredEntry> RemasteredAssetHeaders => _entries;
        public byte[] OriginalData => _originalData;
        public byte[] OriginalRawData => _originalRawData;
        public Dictionary<string, byte[]> RemasteredAssetsDecompressedData => _remasteredAssetsData;
        public Dictionary<string, byte[]> RemasteredAssetsCompressedData => _remasteredAssetsRawData;

        public EgsHdAsset(Header temp_header, byte[] temp_originalData, byte[] temp_originalRawData, byte[] temp_seed)
        {
            _header = temp_header;
            _originalData = temp_originalData;
            _originalRawData = temp_originalRawData;
            _seed = temp_seed;
            _entries = new Dictionary<string, RemasteredEntry>();
            Assets = Array.Empty<string>();
        }

        public EgsHdAsset(Stream stream)
        {
            _stream = stream;
            _baseOffset = stream.Position;

            _seed = Extensions.ReadBytes(stream, 0x10);
            _key = EgsEncryption.GenerateKey(_seed, PASS_COUNT);

            _header = BinaryMapping.ReadObject<Header>(new MemoryStream(_seed));

            var entries = Enumerable
                .Range(0, _header.RemasteredAssetCount)
                .Select(_ => BinaryMapping.ReadObject<RemasteredEntry>(stream))
                .ToList();

            _entries = entries.ToDictionary(x => x.Name, x => x);
            _dataOffset = stream.Position;

            Assets = entries.Select(x => x.Name).ToArray();

            ReadData();

            foreach (var remasteredAssetName in Assets)
            {
                ReadRemasteredAsset(remasteredAssetName);
            }

            Extensions.SetPosition(stream, _dataOffset);
        }

        private byte[] ReadRemasteredAsset(string assetName)
        {
            if (_stream == null || _key == null) return Array.Empty<byte>();
            
            var header = _entries[assetName];
            var dataLength = header.CompressedLength >= 0 ? header.CompressedLength : header.DecompressedLength;

            if (dataLength % 16 != 0)
                dataLength += 16 - (dataLength % 16);

            var data = Extensions.ReadBytes(Extensions.AlignPosition(_stream, 0x10), dataLength);

            _remasteredAssetsRawData.Add(assetName, data.ToArray());

            if (header.CompressedLength > -2)
            {
                for (var i = 0; i < Math.Min(dataLength, 0x100); i += 0x10)
                    EgsEncryption.DecryptChunk(_key, data, i, PASS_COUNT);
            }

            if (header.CompressedLength > -1)
            {
                // Simplified decompression - would need full zlib implementation
                var decompressedData = new byte[header.DecompressedLength];
                // For now, just copy the data
                Array.Copy(data, decompressedData, Math.Min(data.Length, decompressedData.Length));
                data = decompressedData;
            }

            _remasteredAssetsData.Add(assetName, data.ToArray());

            return data;
        }

        private byte[] ReadData()
        {
            if (_stream == null || _key == null) return Array.Empty<byte>();
            
            var dataLength = _header.CompressedLength >= 0 ? _header.CompressedLength : _header.DecompressedLength;
            var data = Extensions.ReadBytes(Extensions.SetPosition(_stream, _dataOffset), dataLength);

            _originalRawData = data.ToArray();

            if (_header.CompressedLength > -2)
            {
                for (var i = 0; i < Math.Min(dataLength, 0x100); i += 0x10)
                    EgsEncryption.DecryptChunk(_key, data, i, PASS_COUNT);
            }

            if (_header.CompressedLength > -1)
            {
                // Simplified decompression
                var decompressedData = new byte[_header.DecompressedLength];
                Array.Copy(data, decompressedData, Math.Min(data.Length, decompressedData.Length));
                data = decompressedData;
            }

            _originalData = data.ToArray();

            return data;
        }
    }
}
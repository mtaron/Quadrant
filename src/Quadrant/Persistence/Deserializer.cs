using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace Quadrant.Persistence
{
    internal sealed class Deserializer : IDisposable
    {
        private readonly StorageFolder _folder;
        private readonly string _name;
        private readonly BinaryReader _reader;

        private Deserializer(StorageFolder folder, string name, uint version, BinaryReader binaryReader)
        {
            _folder = folder;
            _name = name;

            Version = version;
            _reader = binaryReader;
        }

        public uint Version { get; }

        public static async Task DeserializeAsync(StorageFolder folder, string name, Func<Deserializer, Task> deserializeAsync)
        {
            if (File.Exists(Path.Combine(folder.Path, name)))
            {
                using (Stream stream = await folder.OpenStreamForReadAsync(name))
                using (var reader = new BinaryReader(stream))
                {
                    uint version = reader.ReadUInt32();
                    await deserializeAsync(new Deserializer(folder, name, version, reader)).ConfigureAwait(false);
                }
            }
        }

        public async Task ReadInkAsync(InkStrokeContainer strokeContainer)
        {
            strokeContainer.Clear();

            using (Stream stream = await _folder.OpenStreamForReadAsync(_name + Serializer.InkFileExtension))
            {
                await strokeContainer.LoadAsync(stream.AsInputStream());
            }
        }

        public byte ReadByte()
            => _reader.ReadByte();

        public int ReadInt32()
            => _reader.ReadInt32();

        public uint ReadUInt32()
            => _reader.ReadUInt32();

        public float ReadSingle()
            => _reader.ReadSingle();

        public double ReadDouble()
            => _reader.ReadDouble();

        public Vector2 ReadVector2()
        {
            float x = _reader.ReadSingle();
            float y = _reader.ReadSingle();
            return new Vector2(x, y);
        }

        public string ReadString()
            => _reader.ReadString();

        public Color ReadColor()
        {
            byte a = _reader.ReadByte();
            byte r = _reader.ReadByte();
            byte g = _reader.ReadByte();
            byte b = _reader.ReadByte();
            return Color.FromArgb(a, r, g, b);
        }

        public Matrix3x2 ReadMatrix3x2()
        {
            float m11 = _reader.ReadSingle();
            float m12 = _reader.ReadSingle();
            float m21 = _reader.ReadSingle();
            float m22 = _reader.ReadSingle();
            float m31 = _reader.ReadSingle();
            float m32 = _reader.ReadSingle();

            return new Matrix3x2(m11, m12, m21, m22, m31, m32);
        }

        public void Dispose()
            => _reader.Dispose();
    }
}

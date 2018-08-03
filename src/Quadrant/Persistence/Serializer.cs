using System;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Input.Inking;

namespace Quadrant.Persistence
{
    internal sealed class Serializer : IDisposable
    {
        public const string InkFileExtension = ".isf";

        private const uint Version = 1;

        private readonly StorageFolder _folder;
        private readonly string _name;
        private readonly BinaryWriter _writer;

        private Serializer(StorageFolder folder, string name, BinaryWriter binaryWriter)
        {
            _folder = folder;
            _name = name;
            _writer = binaryWriter;
        }

        public static async Task SerializeAsync(StorageFolder folder, string name, Func<Serializer, Task> serializeAsync)
        {
            using (Stream stream = await folder.OpenStreamForWriteAsync(name, CreationCollisionOption.ReplaceExisting))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Version);
                await serializeAsync(new Serializer(folder, name, writer)).ConfigureAwait(false);
            }
        }

        public void Write(byte value)
            => _writer.Write(value);

        public void Write(Color value)
        {
            _writer.Write(value.A);
            _writer.Write(value.R);
            _writer.Write(value.G);
            _writer.Write(value.B);
        }

        public void Write(float value)
            => _writer.Write(value);

        public void Write(Vector2 value)
        {
            _writer.Write(value.X);
            _writer.Write(value.Y);
        }

        public void Write(double value)
            => _writer.Write(value);

        public void Write(int value)
            => _writer.Write(value);

        public void Write(string value)
            => _writer.Write(value);

        public void Write(uint value)
            => _writer.Write(value);

        public void Write(Matrix3x2 matrix)
        {
            _writer.Write(matrix.M11);
            _writer.Write(matrix.M12);
            _writer.Write(matrix.M21);
            _writer.Write(matrix.M22);
            _writer.Write(matrix.M31);
            _writer.Write(matrix.M32);
        }

        public async Task WriteInkAsync(InkStrokeContainer strokeContainer)
        {
            using (Stream stream = await _folder.OpenStreamForWriteAsync(_name + InkFileExtension, CreationCollisionOption.ReplaceExisting))
            {
                await strokeContainer.SaveAsync(stream.AsOutputStream(), InkPersistenceFormat.Isf);
            }
        }

        public void Dispose()
            => _writer.Dispose();
    }
}

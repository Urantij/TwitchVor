using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Utility;

namespace TwitchVor.Space.Local
{
    public class LocalSpaceProvider : BaseSpaceProvider
    {
        readonly string path;

        public FileStream? Fs { get; private set; }

        public override bool AsyncUpload => false;

        public LocalSpaceProvider(Guid guid, string path)
            : base(guid)
        {
            this.path = path;
        }

        public override Task InitAsync()
        {
            string? dir = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            Fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);

            Ready = true;

            return Task.CompletedTask;
        }

        public FileStream OpenReadFs()
        {
            if (Fs != null)
                throw new Exception("fs is not null");

            return Fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        }

        public override async Task PutDataAsync(int id, Stream contentStream, long length)
        {
            if (Fs == null)
                throw new Exception("fs is null");

            await contentStream.CopyStreamAsync(Fs, (int)length);
        }

        public override async Task CloseAsync()
        {
            if (Fs != null)
            {
                await Fs.DisposeAsync();
                Fs = null;
            }
        }

        public override async Task ReadDataAsync(int id, long offset, long length, Stream inputStream)
        {
            if (Fs == null)
            {
                Fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            }

            Fs.Seek(offset, SeekOrigin.Begin);

            await Fs.CopyStreamAsync(inputStream, (int)length);
        }

        public override async Task DestroyAsync()
        {
            if (Fs != null)
            {
                await Fs.DisposeAsync();
                Fs = null;
            }

            File.Delete(path);
        }
    }
}
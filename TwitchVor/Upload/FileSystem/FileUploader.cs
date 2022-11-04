using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Vvideo;

namespace TwitchVor.Upload.FileSystem
{
    class FileUploader : BaseUploader
    {
        private readonly string path;

        public override long SizeLimit => long.MaxValue;
        public override TimeSpan DurationLimit => TimeSpan.MaxValue;

        public FileUploader(Guid guid, string path)
            : base(guid)
        {
            this.path = path;
        }

        public override async Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content)
        {
            string descriptionPath = Path.ChangeExtension(path, "txt");

            await File.WriteAllTextAsync(descriptionPath, $"{name}\n\n\n{description}");

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            await content.CopyToAsync(fs);

            return true;
        }
    }
}
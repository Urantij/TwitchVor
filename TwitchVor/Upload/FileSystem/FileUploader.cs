using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Vvideo;

namespace TwitchVor.Upload.FileSystem
{
    class FileUploader : BaseUploader
    {
        private readonly string path;

        public override long SizeLimit => long.MaxValue;
        public override TimeSpan DurationLimit => TimeSpan.MaxValue;

        public FileUploader(Guid guid, ILoggerFactory loggerFactory, string path)
            : base(guid, loggerFactory)
        {
            this.path = path;
        }

        public override async Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content)
        {
            _logger.LogInformation("Пишем...");

            string descriptionPath = Path.ChangeExtension(path, "txt");

            await File.WriteAllTextAsync(descriptionPath, $"{name}\n\n\n{description}");

            _logger.LogInformation("Записали описание в {path}", descriptionPath);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            await content.CopyToAsync(fs);

            _logger.LogInformation("Записали видево в {path}", path);

            return true;
        }
    }
}
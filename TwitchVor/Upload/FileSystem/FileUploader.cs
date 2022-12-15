using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;
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

        public override async Task<bool> UploadAsync(ProcessingHandler processingHandler, ProcessingVideo video, string name, string description, string fileName, long size, Stream content)
        {
            _logger.LogInformation("Пишем...");

            string descriptionPath = Path.ChangeExtension(path, "txt");

            await File.WriteAllTextAsync(descriptionPath, $"{name}\n\n\n{description}");

            _logger.LogInformation("Записали описание в {path}", descriptionPath);

            using (var context = processingHandler.db.CreateContext())
            {
                var commands = context.ChatMessages
                .Where(m => EF.Functions.Collate(m.Message, Data.StreamDatabase.UTFNoCase).StartsWith("=метка"))
                .Select(m => new
                {
                    m.Username,
                    m.Message,
                    m.PostTime,
                })
                .AsEnumerable()
                .Select(a =>
                {
                    string message = a.Message["=метка".Length..].Trim();

                    TimeSpan time = video.GetOnVideoTime(a.PostTime, processingHandler.skips);

                    if (message.Length > 0)
                    {
                        return $"[{time}] {message} ({a.Username})";
                    }

                    return $"[{time}] ({a.Username})";
                }).ToArray();

                if (commands.Length > 0)
                {
                    string marksPath = Path.ChangeExtension(path, "marks.txt");

                    await File.WriteAllLinesAsync(marksPath, commands);

                    _logger.LogInformation("Записали {count} отметок.", commands.Length);
                }
            }

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            await content.CopyToAsync(fs);

            _logger.LogInformation("Записали видево в {path}", path);

            return true;
        }
    }
}
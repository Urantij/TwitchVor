using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TwitchVor.Data.Models;
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

        public override async Task<bool> UploadAsync(UploaderHandler uploaderHandler, ProcessingVideo video, string name, string description, string fileName, long size, Stream content)
        {
            _logger.LogInformation("Пишем...");

            Task extraTask = WriteExtras(uploaderHandler.processingHandler, video, name, description);

            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

            await content.CopyToAsync(fs);

            await extraTask;

            _logger.LogInformation("Записали видево в {path}", path);

            return true;
        }

        async Task WriteExtras(ProcessingHandler processingHandler, ProcessingVideo video, string name, string description)
        {
            {
                string descriptionPath = Path.ChangeExtension(path, "txt");
                await File.WriteAllTextAsync(descriptionPath, $"{name}\n\n\n{description}");

                _logger.LogInformation("Записали описание в {path}", descriptionPath);
            }

            using var context = processingHandler.db.CreateContext();

            string chatPath = Path.ChangeExtension(path, "chat.txt");
            using var chatFs = new FileStream(chatPath, FileMode.Create);

            string marksPath = Path.ChangeExtension(path, "marks.txt");
            int marksCount = 0;

            const int batchSize = 1000;
            int msgsCount = await context.ChatMessages.CountAsync();
            _logger.LogInformation("Пишем {count} сообщений из чата.", msgsCount);

            for (int msgIndex = 0; msgIndex < msgsCount; msgIndex += batchSize)
            {
                var messages = context.ChatMessages.OrderBy(c => c.Id)
                                                   .Skip(msgIndex)
                                                   .Take(batchSize)
                                                   .ToArray();

                StringBuilder sb = new();
                foreach (var message in messages)
                {
                    sb.Clear();
                    CreateFileMessage(processingHandler, video, message, sb);

                    await chatFs.WriteAsync(Encoding.UTF8.GetBytes(sb.ToString()));
                }

                var commands = messages
                .Where(m => m.Message.StartsWith("=метка", StringComparison.OrdinalIgnoreCase))
                .Select(a =>
                {
                    string message = a.Message["=метка".Length..].TrimStart();

                    TimeSpan time = video.GetOnVideoTime(a.PostTime, processingHandler.skips);

                    if (message.Length > 0)
                    {
                        return $"[{time}] {message} ({a.Username})";
                    }

                    return $"[{time}] ({a.Username})";
                }).ToArray();

                if (commands.Length > 0)
                {
                    marksCount += commands.Length;
                    await File.AppendAllLinesAsync(marksPath, commands);
                }
            }

            if (marksCount > 0)
            {
                _logger.LogInformation("Записали {count} отметок.", marksCount);
            }
        }

        void CreateFileMessage(ProcessingHandler processingHandler, ProcessingVideo video, ChatMessageDb message, StringBuilder sb)
        {
            TimeSpan time = video.GetOnVideoTime(message.PostTime, processingHandler.skips);

            sb.Append('[').Append(time.ToString()).Append(']');

            if (message.Badges != null)
            {
                sb.Append(":b").Append(message.Badges);
            }
            if (message.Color != null)
            {
                sb.Append(":c").Append(message.Color);
            }
            sb.Append(' ');

            if (message.DisplayName != null)
            {
                if (message.DisplayName.Equals(message.Username, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(message.DisplayName);
                }
                else
                {
                    sb.Append(message.Username).Append('/').Append(message.DisplayName);
                }
            }
            else
            {
                sb.Append(message.Username);
            }
            sb.Append(" (").Append(message.UserId).Append(')');

            sb.Append(' ').Append(message.Message);

            sb.Append('\n');
        }
    }
}
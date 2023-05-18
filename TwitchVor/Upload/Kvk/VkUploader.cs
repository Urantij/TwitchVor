using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;
using TwitchVor.Utility;
using VkNet;
using VkNet.Model;

namespace TwitchVor.Upload.Kvk
{
    class VkUploader : BaseUploader
    {
        readonly ILoggerFactory _loggerFactory;

        readonly VkCreds creds;

        public override long SizeLimit => 256L * 1024L * 1024L * 1024L;
        public override TimeSpan DurationLimit => TimeSpan.MaxValue;

        readonly List<VkVideoInfo> uploadedVideos = new();

        public VkUploader(Guid guid, ILoggerFactory loggerFactory, VkCreds creds)
            : base(guid, loggerFactory)
        {
            _loggerFactory = loggerFactory;
            this.creds = creds;
        }

        /// <summary>
        /// Проверит и аплоадера, и волранера.
        /// </summary>
        /// <returns></returns>
        public async Task TestAsync()
        {
            await TestUploaderAsync();
            if (creds.WallRunner != null)
                await TestWallRunnerAsync();
        }

        async Task TestUploaderAsync()
        {
            _logger.LogInformation("Авторизуем аплоадера...");

            using VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.Uploader.ApplicationId,
                AccessToken = creds.Uploader.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            await api.Video.GetAlbumsAsync();

            _logger.LogInformation("Авторизовались.");
        }

        async Task TestWallRunnerAsync()
        {
            if (creds.WallRunner == null)
            {
                throw new NullReferenceException();
            }

            _logger.LogInformation("Авторизуем бегущего по стене...");

            using VkApi vkApi = new();
            await vkApi.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.WallRunner.ApplicationId,
                AccessToken = creds.WallRunner.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.Wall
            });

            await vkApi.Wall.GetAsync(new VkNet.Model.RequestParams.WallGetParams()
            {
                OwnerId = -creds.GroupId,
                Count = 1,
            });

            _logger.LogInformation("Авторизовались.");
        }

        public override async Task<bool> UploadAsync(ProcessingHandler processingHandler, ProcessingVideo video, string name, string description, string fileName, long size, Stream content)
        {
            using var countingContent = new ByteCountingStream(content);

            _logger.LogInformation("Авторизуемся...");

            using VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.Uploader.ApplicationId,
                AccessToken = creds.Uploader.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            _logger.LogInformation("Просим...");

            var saveResult = await api.Video.SaveAsync(new VkNet.Model.RequestParams.VideoSaveParams()
            {
                Name = name,
                Description = description,

                GroupId = creds.GroupId,
            });

            using HttpClient client = new();
            client.Timeout = TimeSpan.FromHours(12);

            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent = new(countingContent);
            streamContent.Headers.ContentLength = size;

            httpContent.Add(streamContent, "video_file", fileName);

            _logger.LogInformation("Начинаем загрузку...");

            var response = await client.PostAsync(saveResult.UploadUrl, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Не удалось завершить загрузку. {content}", responseContent);
                return false;
            }

            _logger.LogInformation("Закончили загрузку.");

            PostUpload(processingHandler, video, saveResult);

            return true;
        }

        // Обманом заставить вк есть видос неизвестного размера.
        public async Task<bool> UploadUnknownAsync(ProcessingHandler processingHandler, ProcessingVideo video, string name, string description, string fileName, long size, Stream content)
        {
            using var countingContent = new ByteCountingStream(content);

            _logger.LogInformation("Авторизуемся...");

            using VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.Uploader.ApplicationId,
                AccessToken = creds.Uploader.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            _logger.LogInformation("Просим...");

            var saveResult = await api.Video.SaveAsync(new VkNet.Model.RequestParams.VideoSaveParams()
            {
                Name = name,
                Description = description,

                GroupId = creds.GroupId,
            });

            // Сюда пишем то, что будет читать аплоадер.
            // Когда закончим писать, его нужно будет закрыть.
            using var serverTrashPipe = new AnonymousPipeServerStream(PipeDirection.Out);
            // Это читает аплоадер.
            using var clientTrashPipe = new AnonymousPipeClientStream(PipeDirection.In, serverTrashPipe.ClientSafePipeHandle);
            using var clientTrashNotification = new NotifiableStream(clientTrashPipe);

            // Смотри #29 
            long baseSize = CalculateBaseSize(fileName);

            using HttpClient client = new();
            client.Timeout = TimeSpan.FromHours(12);

            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent = new(countingContent);
            using StreamContent streamFakeContent = new(clientTrashNotification);

            httpContent.Add(streamContent, "video_file", fileName);
            httpContent.Add(streamFakeContent, "garbage", "helpme.txt");

            streamFakeContent.Headers.ContentLength = null;

            long declaredSize = baseSize + size;

            httpContent.Headers.ContentLength = declaredSize;

            clientTrashNotification.FirstReaded += () => Task.Run(async () =>
            {
                long needToWrite = declaredSize - countingContent.TotalBytesRead - baseSize;

                try
                {
                    await SpamTrashAsync(serverTrashPipe, needToWrite);
                    await serverTrashPipe.FlushAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при спаме в мусорку.");
                }
                finally
                {
                    await serverTrashPipe.DisposeAsync();
                }
            });

            _logger.LogInformation("Начинаем загрузку...");

            var response = await client.PostAsync(saveResult.UploadUrl, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Не удалось завершить загрузку. {content}", responseContent);
                return false;
            }

            _logger.LogInformation("Закончили загрузку.");

            PostUpload(processingHandler, video, saveResult);

            return true;
        }

        void PostUpload(ProcessingHandler processingHandler, ProcessingVideo video, VkNet.Model.Attachments.Video saveResult)
        {
            if (saveResult.Id != null)
            {
                VkVideoInfo vkVideo = new(video, saveResult.Id.Value);
                uploadedVideos.Add(vkVideo);

                _ = Task.Run(async () =>
                {
                    await processingHandler.ProcessTask;

                    await PostUploadDescriptionUpdate(processingHandler, vkVideo);
                });

                if (creds.WallRunner != null && processingHandler.videos.FirstOrDefault() == video)
                {
                    // Пусть только первый видос запускает постобработку.
                    // И всё видосы одним постом выложатся.
                    _ = Task.Run(async () =>
                    {
                        _logger.LogInformation("Запущен постобработчик.");

                        await processingHandler.ProcessTask;

                        await PostCringeAsync();
                    });
                }
            }
            else
            {
                _logger.LogCritical("Id видео нулл");
            }
        }

        async Task PostUploadDescriptionUpdate(ProcessingHandler processingHandler, VkVideoInfo vkVideoInfo)
        {
            if (vkVideoInfo.video.success != true)
                return;

            await Task.Delay(TimeSpan.FromSeconds(10));

            _logger.LogInformation("Авторизуемся...");

            using VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.Uploader.ApplicationId,
                AccessToken = creds.Uploader.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            _logger.LogInformation("Меняем описание (id)...", vkVideoInfo.id);

            string name = processingHandler.MakeVideoName(vkVideoInfo.video);
            string description = processingHandler.MakeVideoDescription(vkVideoInfo.video);

            try
            {
                await api.Video.EditAsync(new VkNet.Model.RequestParams.VideoEditParams()
                {
                    OwnerId = -creds.GroupId,

                    VideoId = vkVideoInfo.id,

                    Name = name,
                    Desc = description
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Не удалось обновить описание видео {id}.", vkVideoInfo.id);
                return;
            }

            _logger.LogInformation("Изменили описание видео {id}.", vkVideoInfo.id);
        }

        /// <summary>
        /// Дождать загрузки и выложить пост на стену группы.
        /// </summary>
        /// <returns></returns>
        async Task PostCringeAsync()
        {
            VkVideoInfo[] videoInfos = uploadedVideos.Where(v => v.video.success == true).ToArray();

            if (videoInfos.Length == 0)
            {
                _logger.LogWarning("Постобработка нашла 0 видео.");
                return;
            }

            VkWaller waller = new(_loggerFactory, creds);
            try
            {
                await waller.MakePostAsync(videoInfos.Select(i => i.id).ToArray());
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Не удалось сделать пост.");
                return;
            }

            _logger.LogInformation("Запостили кринж в вк.");
        }

        async Task SpamTrashAsync(Stream target, long needToWrite)
        {
            byte[] bytes = Encoding.UTF8.GetBytes("Я правда не хочу этого делать, это такой костыль, просто ужас. Но мне нужно загрузить видео неизвестного размера, и иного варианта я не вижу.");

            long written = 0;
            _logger.LogInformation("Нужно сделать {bytes} мусора", needToWrite);

            DateTime data = DateTime.MinValue;
            while (written < needToWrite)
            {
                var passed = DateTime.UtcNow - data;
                if (passed > TimeSpan.FromSeconds(5))
                {
                    data = DateTime.UtcNow;
                    _logger.LogInformation("пишем мусор {n}/{n2}", written, needToWrite);
                }

                long toWrite = Math.Min(needToWrite - written, bytes.Length);

                await target.WriteAsync(bytes.AsMemory(0, (int)toWrite));

                written += toWrite;
            }

            _logger.LogInformation("конец мусора");
        }

        static long CalculateBaseSize(string fileName)
        {
            using MemoryStream ms = new();
            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent1 = new(ms);
            using StreamContent streamContent2 = new(ms);

            httpContent.Add(streamContent1, "video_file", fileName);
            httpContent.Add(streamContent2, "garbage", "helpme.txt");

            return httpContent.Headers.ContentLength!.Value;
        }
    }
}
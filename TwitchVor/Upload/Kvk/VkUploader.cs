using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;
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
            client.Timeout = TimeSpan.FromHours(4);

            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent = new(content);

            httpContent.Add(streamContent, "video_file", fileName);

            httpContent.Headers.ContentLength = size + 185;

            _logger.LogInformation("Начинаем загрузку...");

            var response = await client.PostAsync(saveResult.UploadUrl, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Не удалось завершить загрузку. {content}", responseContent);
                return false;
            }

            _logger.LogInformation("Закончили загрузку.");

            if (saveResult.Id != null)
            {
                lock (processingHandler.trashcan)
                    processingHandler.trashcan.Add(new VkVideoInfo(video, saveResult.Id.Value));
            }
            else
            {
                _logger.LogCritical("Id видео нулл");
            }

            if (creds.WallRunner != null && processingHandler.videos.FirstOrDefault() == video)
            {
                // Пусть только первый видос запускает постобработку.
                // И всё видосы одним постом выложатся.

                VkWaller waller = new(_loggerFactory, creds);

                _ = Task.Run(async () =>
                {
                    _logger.LogInformation("Запущен постобработчик.");

                    await processingHandler.ProcessTask;

                    VkVideoInfo[] videoInfos;
                    lock (processingHandler.trashcan)
                        videoInfos = processingHandler.trashcan.OfType<VkVideoInfo>().Where(v => v.video.success == true).ToArray();

                    if (videoInfos.Length == 0)
                    {
                        _logger.LogWarning("Постобработка нашла 0 видео.");
                        return;
                    }

                    try
                    {
                        await waller.MakePostAsync(videoInfos.Select(i => i.id).ToArray());
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Не удалось сделать пост.");
                    }
                });
            }

            return true;
        }
    }
}
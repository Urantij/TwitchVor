using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VkNet;
using VkNet.Model;

namespace TwitchVor.Upload.Kvk
{
    class VkUploader : BaseUploader
    {
        readonly VkCreds creds;

        public override long SizeLimit => 256L * 1024L * 1024L * 1024L;
        public override TimeSpan DurationLimit => TimeSpan.MaxValue;

        public VkUploader(Guid guid, ILoggerFactory loggerFactory, VkCreds creds)
            : base(guid, loggerFactory)
        {
            this.creds = creds;
        }

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

        public override async Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content)
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

            return true;
        }
    }
}
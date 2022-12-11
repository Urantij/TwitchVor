using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
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

            // Создадим файл с мусорным контентом.
            string trashContentPath = Path.Combine(Program.config.CacheDirectoryName, guid.ToString("N") + ".trashupload");

            using (var trashFs = new FileStream(trashContentPath, FileMode.Create))
            {
                const int targetSize = 1024 * 1024;

                byte[] bytes = Encoding.UTF8.GetBytes("Я правда не хочу этого делать, это такой костыль, просто ужас. Но мне нужно загрузить видео неизвестного размера, и иного варианта я не вижу.");

                int written = 0;
                while (written < targetSize)
                {
                    await trashFs.WriteAsync(bytes);
                    written += bytes.Length;
                }
            }

            using var readTrashFs = new FileStream(trashContentPath, FileMode.Open);

            // Смотри #29 
            long baseSize = CalculateBaseSize();

            using HttpClient client = new();
            client.Timeout = TimeSpan.FromHours(4);

            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent = new(content);
            using StreamContent streamFakeContent = new(readTrashFs);

            httpContent.Add(streamContent, "video_file", fileName);
            httpContent.Add(streamFakeContent, "garbage", "helpme.txt");

            streamFakeContent.Headers.ContentLength = null;

            httpContent.Headers.ContentLength = baseSize + size;

            _logger.LogInformation("Начинаем загрузку...");

            try
            {
                var response = await client.PostAsync(saveResult.UploadUrl, httpContent);

                if (!response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Не удалось завершить загрузку. {content}", responseContent);
                    return false;
                }
            }
            catch (System.Net.Http.HttpRequestException e) when (e.Message.Contains(" request content bytes, but Content-Length promised "))
            {
                // Это благоприятный для нас расклад.
                // Ещё было бы неплохо узнать, задиспоужен ли контент.
            }
            catch
            {
                throw;
            }
            finally
            {
                File.Delete(trashContentPath);
            }

            _logger.LogInformation("Закончили загрузку.");

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

            return true;
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

        static long CalculateBaseSize()
        {
            using MemoryStream ms = new();
            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent1 = new(ms);
            using StreamContent streamContent2 = new(ms);

            httpContent.Add(streamContent1, "video_file", "result.mp4");
            httpContent.Add(streamContent2, "garbage", "helpme.txt");

            return httpContent.Headers.ContentLength!.Value;
        }
    }
}
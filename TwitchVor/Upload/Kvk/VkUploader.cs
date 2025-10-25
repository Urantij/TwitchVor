using System.IO.Pipes;
using System.Text;
using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;
using TwitchVor.Utility;
using VkNet;
using VkNet.Model;

namespace TwitchVor.Upload.Kvk;

internal class VkUploader : BaseUploader
{
    private readonly ILoggerFactory _loggerFactory;

    private readonly VkCreds _creds;

    public override long SizeLimit => 256L * 1024L * 1024L * 1024L;
    public override TimeSpan DurationLimit => TimeSpan.MaxValue;

    private readonly List<VkVideoInfo> _uploadedVideos = new();

    private Task? _postUploadTask = null;

    public VkUploader(Guid guid, ILoggerFactory loggerFactory, VkCreds creds)
        : base(guid, loggerFactory)
    {
        _loggerFactory = loggerFactory;
        this._creds = creds;
    }

    /// <summary>
    /// Проверит и аплоадера, и волранера.
    /// </summary>
    /// <returns></returns>
    public async Task TestAsync()
    {
        await TestUploaderAsync();
        if (_creds.WallRunner != null)
            // Да, библиотека настолько всратая
            try
            {
                await TestWallRunnerAsync();
            }
            catch (Newtonsoft.Json.JsonSerializationException e) when (e.Message.StartsWith("Error converting value \"base\" to type"))
            {
                _logger.LogWarning("Клоунаду так и не исправили.");
            }
    }

    private async Task TestUploaderAsync()
    {
        _logger.LogInformation("Авторизуем аплоадера...");

        using VkApi api = new();
        await api.AuthorizeAsync(new ApiAuthParams()
        {
            ApplicationId = _creds.Uploader.ApplicationId,
            AccessToken = _creds.Uploader.ApiToken,
            Settings = VkNet.Enums.Filters.Settings.All
        });

        await api.Video.GetAlbumsAsync();

        _logger.LogInformation("Авторизовались.");
    }

    private async Task TestWallRunnerAsync()
    {
        if (_creds.WallRunner == null)
        {
            throw new NullReferenceException();
        }

        _logger.LogInformation("Авторизуем бегущего по стене...");

        using VkApi vkApi = new();
        await vkApi.AuthorizeAsync(new ApiAuthParams()
        {
            ApplicationId = _creds.WallRunner.ApplicationId,
            AccessToken = _creds.WallRunner.ApiToken,
            Settings = VkNet.Enums.Filters.Settings.Wall
        });

        await vkApi.Wall.GetAsync(new VkNet.Model.WallGetParams()
        {
            OwnerId = -_creds.GroupId,
            Count = 1,
        });

        _logger.LogInformation("Авторизовались.");
    }

    public override async Task<bool> UploadAsync(UploaderHandler uploaderHandler, ProcessingVideo video,
        string name, string description, string fileName, long size, Stream content)
    {
        using var countingContent = new ByteCountingStream(content);

        _logger.LogInformation("Авторизуемся...");

        using VkApi api = new();
        await api.AuthorizeAsync(new ApiAuthParams()
        {
            ApplicationId = _creds.Uploader.ApplicationId,
            AccessToken = _creds.Uploader.ApiToken,
            Settings = VkNet.Enums.Filters.Settings.All
        });

        _logger.LogInformation("Просим...");

        Video saveResult = await api.Video.SaveAsync(new VkNet.Model.VideoSaveParams()
        {
            Name = name,
            Description = description,

            GroupId = _creds.GroupId,
        });

        using HttpClient client = new();
        client.Timeout = TimeSpan.FromHours(12);

        using MultipartFormDataContent httpContent = new();
        using StreamContent streamContent = new(countingContent);
        streamContent.Headers.ContentLength = null;

        httpContent.Add(streamContent, "video_file", fileName);

        httpContent.Headers.ContentLength = CalculateBaseSize(fileName) + size;

        _logger.LogInformation("Начинаем загрузку...");

        HttpResponseMessage response = await client.PostAsync(saveResult.UploadUrl, httpContent);

        if (!response.IsSuccessStatusCode)
        {
            string responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Не удалось завершить загрузку. {content}", responseContent);
            return false;
        }

        _logger.LogInformation("Закончили загрузку.");

        PostUpload(uploaderHandler, video, saveResult);

        return true;
    }

    // Обманом заставить вк есть видос неизвестного размера.
    public async Task<bool> UploadUnknownAsync(UploaderHandler uploaderHandler, ProcessingVideo video, string name,
        string description, string fileName, long size, Stream content)
    {
        using var countingContent = new ByteCountingStream(content);

        _logger.LogInformation("Авторизуемся...");

        using VkApi api = new();
        await api.AuthorizeAsync(new ApiAuthParams()
        {
            ApplicationId = _creds.Uploader.ApplicationId,
            AccessToken = _creds.Uploader.ApiToken,
            Settings = VkNet.Enums.Filters.Settings.All
        });

        _logger.LogInformation("Просим...");

        var saveResult = await api.Video.SaveAsync(new VkNet.Model.VideoSaveParams()
        {
            Name = name,
            Description = description,

            GroupId = _creds.GroupId,
        });

        // Сюда пишем то, что будет читать аплоадер.
        // Когда закончим писать, его нужно будет закрыть.
        using var serverTrashPipe = new AnonymousPipeServerStream(PipeDirection.Out);
        // Это читает аплоадер.
        using var clientTrashPipe =
            new AnonymousPipeClientStream(PipeDirection.In, serverTrashPipe.ClientSafePipeHandle);
        using var clientTrashNotification = new NotifiableStream(clientTrashPipe);

        // Смотри #29 
        long baseSize = CalculateBaseSizeForUnknown(fileName);

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

        PostUpload(uploaderHandler, video, saveResult);

        return true;
    }

    private void PostUpload(UploaderHandler uploaderHandler, ProcessingVideo video, VkNet.Model.Video saveResult)
    {
        if (saveResult.Id != null)
        {
            VkVideoInfo vkVideo = new(video, saveResult.Id.Value);
            _uploadedVideos.Add(vkVideo);

            if (_postUploadTask == null)
                _postUploadTask = PostProcessWorkAsync(uploaderHandler);
        }
        else
        {
            _logger.LogCritical("Id видео нулл");
        }
    }

    private async Task PostProcessWorkAsync(UploaderHandler uploaderHandler)
    {
        await uploaderHandler.ProcessTask;
        await Task.Delay(TimeSpan.FromSeconds(10));

        _logger.LogInformation("Запущен первый постобработчик.");

        // В теории, делать имя поста нужно из всех игр всех видиков
        // Но мне чета так впадлу это писать.
        // Ща проверил. Оно и так пишет все игры со стрима, а не на конкретном видике. хд
        string postText = DescriptionMaker.FormVideoName(
            uploaderHandler.processingHandler.handlerCreationDate,
            null, 200, uploaderHandler.processingHandler.timestamps);
        await PostCringeAsync(postText);

        // Описание меняем после полного завершения.

        await uploaderHandler.processingHandler.ProcessTask;
        await Task.Delay(TimeSpan.FromSeconds(10));

        _logger.LogInformation("Запущен второй постобработчик.");

        foreach (VkVideoInfo vkVideo in _uploadedVideos)
        {
            await PostProcessDescriptionUpdate(uploaderHandler, vkVideo);
        }
    }

    private async Task PostProcessDescriptionUpdate(UploaderHandler uploaderHandler, VkVideoInfo vkVideoInfo)
    {
        if (vkVideoInfo.video.success != true)
            return;

        try
        {
            _logger.LogInformation("Авторизуемся...");

            using VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = _creds.Uploader.ApplicationId,
                AccessToken = _creds.Uploader.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            _logger.LogInformation("Меняем описание ({id})...", vkVideoInfo.id);

            // Видео всегда должно там лежать
            int videoIndex = uploaderHandler.videos.Index().First(v => v.Item == vkVideoInfo.video).Index;

            string? nextVideoUrl = FindRelatedVideo(uploaderHandler, videoIndex + 1, _uploadedVideos, v => v.video)
                ?.ToLink(_creds.GroupId);
            string? prevVideoUrl = FindRelatedVideo(uploaderHandler, videoIndex - 1, _uploadedVideos, v => v.video)
                ?.ToLink(_creds.GroupId);

            string name = uploaderHandler.MakeVideoName(vkVideoInfo.video);
            string description = uploaderHandler.MakeVideoDescription(vkVideoInfo.video, nextVideoUrl: nextVideoUrl,
                prevVideoUrl: prevVideoUrl);

            await api.Video.EditAsync(new VkNet.Model.VideoEditParams()
            {
                OwnerId = -_creds.GroupId,

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
    /// Выложить пост на стену группы.
    /// </summary>
    /// <returns></returns>
    private async Task PostCringeAsync(string? postText)
    {
        VkVideoInfo[] videoInfos = _uploadedVideos.Where(v => v.video.success == true).ToArray();

        if (videoInfos.Length == 0)
        {
            _logger.LogWarning("Постобработка нашла 0 видео.");
            return;
        }

        VkWaller waller = new(_loggerFactory, _creds);
        try
        {
            await waller.MakePostAsync(postText, videoInfos.Select(i => i.id).ToArray());
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Не удалось сделать пост.");
            return;
        }

        _logger.LogInformation("Запостили кринж в вк.");
    }

    private async Task SpamTrashAsync(Stream target, long needToWrite)
    {
        byte[] bytes =
            Encoding.UTF8.GetBytes(
                "Я правда не хочу этого делать, это такой костыль, просто ужас. Но мне нужно загрузить видео неизвестного размера, и иного варианта я не вижу.");

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

    private static long CalculateBaseSize(string fileName)
    {
        using MemoryStream ms = new();
        using MultipartFormDataContent httpContent = new();
        using StreamContent streamContent1 = new(ms);

        httpContent.Add(streamContent1, "video_file", fileName);

        return httpContent.Headers.ContentLength!.Value;
    }

    private static long CalculateBaseSizeForUnknown(string fileName)
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
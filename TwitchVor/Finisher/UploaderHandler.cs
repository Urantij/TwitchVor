using TwitchVor.Upload;

namespace TwitchVor.Finisher;

/// <summary>
/// Следит за процессом обработки стрима одним из загрузчиков.
/// </summary>
internal class UploaderHandler
{
    private readonly TaskCompletionSource processTCS = new();

    public readonly BaseUploader uploader;

    public readonly ProcessingHandler processingHandler;

    public readonly IReadOnlyCollection<ProcessingVideo> videos;

    /// <summary>
    /// Завершается, когда закончил работать загрузчик.
    /// </summary>
    public Task ProcessTask => processTCS.Task;

    public UploaderHandler(BaseUploader uploader, ProcessingHandler processingHandler,
        IReadOnlyCollection<ProcessingVideo> videos)
    {
        this.uploader = uploader;
        this.processingHandler = processingHandler;
        this.videos = videos;
    }

    public void SetResult()
    {
        processTCS.SetResult();
    }

    public string MakeVideoName(ProcessingVideo video, int lengthLimit = 100)
    {
        return DescriptionMaker.FormVideoName(processingHandler.handlerCreationDate,
            videos.Count == 1 ? null : video.number, lengthLimit, processingHandler.timestamps);
    }

    public string MakeVideoDescription(ProcessingVideo video, string? nextVideoUrl = null, string? prevVideoUrl = null)
    {
        TimeSpan? videoUploadTime = video.uploadEnd - video.uploadStart;
        TimeSpan? totalUploadTime = SumTotalUploadTime();

        return DescriptionMaker.FormDescription(video.startDate, processingHandler.timestamps, processingHandler.skips,
            processingHandler.subgifters, processingHandler.advertismentLoss, processingHandler.totalLoss,
            processingHandler.bills, videoUploadTime, totalUploadTime, nextVideoUrl: nextVideoUrl, prevVideoUrl: prevVideoUrl);
    }

    private TimeSpan? SumTotalUploadTime()
    {
        var uploads = videos.Where(v => v.uploadStart != null && v.uploadEnd != null)
            .Select(v => (v.uploadEnd!.Value - v.uploadStart!.Value).Ticks)
            .ToArray();

        if (uploads.Length == 0)
            return null;

        return TimeSpan.FromTicks(uploads.Sum());
    }
}
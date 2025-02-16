using Microsoft.Extensions.Logging;
using TwitchVor.Finisher;

namespace TwitchVor.Upload;

internal abstract class BaseUploader
{
    protected readonly ILogger _logger;

    protected Guid guid;

    /// <summary>
    /// В байтах
    /// </summary>
    public abstract long SizeLimit { get; }

    public abstract TimeSpan DurationLimit { get; }

    public virtual bool NeedsExactVideoSize => true;

    protected BaseUploader(Guid guid, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        this.guid = guid;
    }

    public abstract Task<bool> UploadAsync(UploaderHandler uploaderHandler, ProcessingVideo video, string name,
        string description, string fileName, long size, Stream content);

    protected static T? FindRelatedVideo<T>(UploaderHandler uploaderHandler, int index, IReadOnlyList<T> list,
        Func<T, ProcessingVideo> extractor) where T : class
    {
        ProcessingVideo? processingVideo = uploaderHandler.videos.ElementAtOrDefault(index);

        if (processingVideo == null) return null;

        T? video =
            list.FirstOrDefault(v => extractor(v) == processingVideo);

        return video;
    }
}
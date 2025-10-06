using Microsoft.Extensions.Logging;
using TwitchStreamDownloader.Download;
using TwitchVor.Data;
using TwitchVor.Data.Models;

namespace TwitchVor.Twitch.Downloader;

/// <summary>
/// Хранит информацию о видео форматах в стриме
/// </summary>
public class FormatContainer
{
    private readonly StreamDatabase _database;
    private readonly ILogger _logger;

    private readonly List<VideoFormatDb> _formats = [];

    public FormatContainer(StreamDatabase database, ILogger logger)
    {
        _database = database;
        _logger = logger;
    }

    public VideoFormatDb GetVideoFormatByQuality(Quality quality)
    {
        int fps = (int)Math.Round(quality.Fps);

        VideoFormatDb? result;

        lock (_formats)
        {
            result = _formats.FirstOrDefault(f =>
                f.Fps == fps && f.Width == quality.Resolution.Width && f.Height == quality.Resolution.Height);

            if (result == null)
            {
                result = _database.AddVideoFormatAsync(quality).GetAwaiter().GetResult();

                _formats.Add(result);

                _logger.LogInformation("Добавили формат {width}:{height} {fps}", quality.Resolution.Width,
                    quality.Resolution.Height, fps);
            }
        }

        return result;
    }
}
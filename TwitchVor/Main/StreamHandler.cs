using Microsoft.Extensions.Logging;
using TwitchVor.Data;
using TwitchVor.Space;
using TwitchVor.Twitch;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Utility;
using TwitchVor.Vvideo;

namespace TwitchVor.Main;

/// <summary>
/// Хранит информацию о текущем стриме.
/// </summary>
internal class StreamHandler
{
    internal bool Finished { get; private set; } = false;
    internal bool Suspended { get; private set; } = false;

    private readonly ILogger _logger;

    public readonly Guid guid;

    public readonly StreamDatabase db;

    public readonly BaseSpaceProvider space;

    public readonly StreamDownloader streamDownloader;

    public readonly Timestamper timestamper;

    public readonly StreamChatWorker chatWorker;

    internal SubCheck? subCheck;

    /// <summary>
    /// UTC
    /// </summary>
    internal readonly DateTime handlerCreationDate;

    public StreamHandler(Timestamper timestamper, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        guid = Guid.NewGuid();

        this.timestamper = timestamper;

        handlerCreationDate = DateTime.UtcNow;

        db = new StreamDatabase(MakeDbPath(guid));

        space = DependencyProvider.GetSpaceProvider(guid, loggerFactory);

        streamDownloader = new StreamDownloader(guid, db, space, loggerFactory);

        chatWorker = new StreamChatWorker(this, loggerFactory);
    }

    internal async Task StartAsync()
    {
        _logger.LogInformation("Starting...");

        await db.InitAsync();

        if (Program.chatBot != null)
        {
            Program.chatBot.client.PrivateMessageReceived += chatWorker.PrivateMessageReceived;
        }

        _ = space.InitAsync();

        streamDownloader.Start();

        if (Program.subChecker != null)
        {
            _ = Task.Run(async () =>
            {
                SubCheck? currentSubCheck = null;

                while (currentSubCheck == null && !Finished)
                {
                    currentSubCheck = await Program.subChecker.GetSubAsync();

                    if (currentSubCheck != null)
                    {
                        subCheck = currentSubCheck;
                        return;
                    }

                    TimeSpan delay = TimeSpan.FromMinutes(30);

                    _logger.LogWarning("Не удалось получить инфу о сабке, продолжим через {minutes:N0}",
                        delay.TotalMinutes);
                    await Task.Delay(delay);
                }
            });
        }
    }

    /// <summary>
    /// Остановим загрузчики от ддоса твича
    /// </summary>
    internal void Suspend()
    {
        _logger.LogInformation("Suspending...");

        Suspended = true;

        streamDownloader.Suspend();
    }

    /// <summary>
    /// Загрузчики должны были умереть, так что запустим их
    /// </summary>
    internal void Resume()
    {
        _logger.LogInformation("Resuming...");

        Suspended = false;

        streamDownloader.Resume();
    }

    /// <summary>
    /// Конец.
    /// </summary>
    internal async Task FinishAsync()
    {
        _logger.LogInformation("Finishing...");

        Finished = true;

        if (Program.chatBot != null)
        {
            Program.chatBot.client.PrivateMessageReceived -= chatWorker.PrivateMessageReceived;
        }

        await streamDownloader.CloseAsync();
    }

    internal async Task DestroyAsync(bool destroySegments, bool destroyDB)
    {
        if (destroyDB)
        {
            await db.DestroyAsync();
        }

        if (destroySegments)
        {
            await space.DestroyAsync();
        }
    }

    private static string MakeDbPath(Guid guid)
    {
        return Path.Combine(Program.config.CacheDirectoryName, guid.ToString("N") + ".sqlite");
    }
}
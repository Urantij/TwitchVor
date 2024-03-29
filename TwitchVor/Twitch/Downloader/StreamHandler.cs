using System.Linq;
using System.Net.Http;
using ExtM3UPlaylistParser.Models;
using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchSimpleLib.Chat.Messages;
using TwitchStreamDownloader.Download;
using TwitchStreamDownloader.Exceptions;
using TwitchStreamDownloader.Net;
using TwitchStreamDownloader.Queues;
using TwitchStreamDownloader.Resources;
using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.Finisher;
using TwitchVor.Space;
using TwitchVor.Space.Local;
using TwitchVor.Twitch.Chat;
using TwitchVor.Twitch.Checker;
using TwitchVor.Utility;
using TwitchVor.Vvideo;

namespace TwitchVor.Twitch.Downloader
{
    /// <summary>
    /// Хранит информацию о текущем стриме.
    /// </summary>
    class StreamHandler
    {
        internal bool Finished { get; private set; } = false;
        internal bool Suspended { get; private set; } = false;

        readonly ILogger _logger;

        public readonly Guid guid;

        public readonly StreamDatabase db;

        public readonly BaseSpaceProvider space;

        public readonly StreamDownloader streamDownloader;

        public readonly Timestamper timestamper;
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
        }

        internal async Task StartAsync()
        {
            _logger.LogInformation("Starting...");

            await db.InitAsync();

            if (Program.chatBot != null)
            {
                Program.chatBot.client.PrivateMessageReceived += PrivateMessageReceived;
            }

            _ = space.InitAsync();

            streamDownloader.Start();

            if (Program.subChecker != null)
            {
                _ = Task.Run(async () =>
                {
                    SubCheck? subCheck = null;

                    while (subCheck == null && !Finished)
                    {
                        subCheck = await Program.subChecker.GetSubAsync();

                        if (subCheck != null)
                        {
                            this.subCheck = subCheck;
                            return;
                        }
                        else
                        {
                            TimeSpan delay = TimeSpan.FromMinutes(30);

                            _logger.LogWarning("Не удалось получить инфу о сабке, продолжим через {minutes:N0}", delay.TotalMinutes);
                            await Task.Delay(delay);
                        }
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
                Program.chatBot.client.PrivateMessageReceived -= PrivateMessageReceived;
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

        private async void PrivateMessageReceived(object? sender, TwitchPrivateMessage priv)
        {
            try
            {
                string? badges = priv.rawIrcMessage.tags?.GetValueOrDefault("badges");

                await db.AddChatMessageAsync(priv.userId, priv.username, priv.displayName, priv.text, priv.color, badges, priv.tmiSentTs);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PrivMsgReceived");
            }

            try
            {
                // Временная мера, которая будет постоянной, потому что чатбота нормального у меня нет.
                if (priv.mod && priv.text.StartsWith("=метка ", StringComparison.OrdinalIgnoreCase))
                {
                    string text = priv.text["=метка ".Length..];

                    timestamper.AddTimestamp(new ChatCustomTimestamp(text, priv.displayName ?? priv.username, DateTime.UtcNow));
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PrivMsgReceived");
            }
        }

        static string MakeDbPath(Guid guid)
        {
            return Path.Combine(Program.config.CacheDirectoryName, guid.ToString("N") + ".sqlite");
        }
    }
}
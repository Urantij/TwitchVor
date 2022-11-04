using System.Linq;
using System.Net.Http;
using ExtM3UPlaylistParser.Models;
using TwitchLib.Api;
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

        public StreamHandler(Timestamper timestamper)
        {
            guid = Guid.NewGuid();

            this.timestamper = timestamper;

            handlerCreationDate = DateTime.UtcNow;

            db = new StreamDatabase(MakeDbPath(guid));

            space = DependencyProvider.GetSpaceProvider(guid);
            
            streamDownloader = new StreamDownloader(guid, db, space);
        }

        void Log(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.Log(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        void LogWarning(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogWarning(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        void LogError(string message)
        {
            //TODO сделать норм идентификатор
            ColorLog.LogError(message, $"StreamHandler{handlerCreationDate:ss}");
        }

        internal async Task StartAsync()
        {
            Log("Starting...");

            await db.InitAsync();

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
                            await Task.Delay(TimeSpan.FromMinutes(30));
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
            Log("Suspending...");

            Suspended = true;

            streamDownloader.Suspend();
        }

        /// <summary>
        /// Загрузчики должны были умереть, так что запустим их
        /// </summary>
        internal void Resume()
        {
            Log("Resuming...");

            Suspended = false;

            streamDownloader.Resume();
        }

        /// <summary>
        /// Конец.
        /// </summary>
        internal async Task FinishAsync()
        {
            Log("Finishing...");

            Finished = true;

            await streamDownloader.CloseAsync();
        }

        internal async Task DestroyAsync(bool destroySpace)
        {
            await db.DestroyAsync();

            if (destroySpace)
            {
                await space.DestroyAsync();
            }
        }

        static string MakeDbPath(Guid guid)
        {
            return Path.Combine(Program.config.CacheDirectoryName, guid.ToString("N") + ".sqlite");
        }
    }
}
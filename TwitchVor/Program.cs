using System;
using System.Drawing;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TimeWebNet;
using TwitchLib.Api;
using TwitchVor.Communication.Email;
using TwitchVor.Configuration;
using TwitchVor.Conversion;
using TwitchVor.Finisher;
using TwitchVor.Twitch;
using TwitchVor.Twitch.Checker;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Utility;
using TwitchVor.Vvideo.Money;
using static TwitchVor.Utility.ColoredConsoleOptions;

namespace TwitchVor
{
    class Program
    {
        const string configPath = "config.json";

#nullable disable
        public static TwitchAPI twitchAPI;

        public static TwitchStatuser statuser;

        public static StreamsManager streamsManager;

        public static Config config;
#nullable enable

        public static SubChecker? subChecker;
        public static Ffmpeg? ffmpeg;

        public static Emailer? emailer;

        public static bool debug = false;
        public static bool shutdown = false;

        static async Task Main(string[] appArgs)
        {
            debug = appArgs.Contains("--debug");

#if DEBUG
            {
                debug = true;
            }
#endif

            using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.Services.Add(new Microsoft.Extensions.DependencyInjection.ServiceDescriptor(typeof(ColoredConsoleOptions), typeof(ColoredConsoleOptions), Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton));

                builder.AddConsoleFormatter<ColoredConsoleFormatter, ColoredConsoleOptions>(options =>
                {
                    options.TimestampFormat = "[HH:mm:ss] ";

                    options.Colors = new List<ColoredCategory>()
                    {
                        new ColoredCategory(typeof(Greater), ConsoleColor.Yellow, ConsoleColor.DarkYellow),

                        new ColoredCategory(typeof(Upload.Kvk.VkUploader), ConsoleColor.White, ConsoleColor.DarkBlue),
                        new ColoredCategory(typeof(Upload.Kvk.VkWaller), ConsoleColor.White, ConsoleColor.DarkBlue),

                        new ColoredCategory(typeof(Space.Local.LocalSpaceProvider), ConsoleColor.DarkGreen),
                        new ColoredCategory(typeof(Space.TimeWeb.TimewebSpaceProvider), ConsoleColor.DarkMagenta, ConsoleColor.Gray),

                        new ColoredCategory(typeof(StreamDownloader), ConsoleColor.Magenta),
                    };
                });
                builder.AddConsole(b => b.FormatterName = nameof(ColoredConsoleFormatter));

                if (debug)
                    builder.SetMinimumLevel(LogLevel.Debug);
            });

            Greater.SetLogger(loggerFactory);
            DescriptionMaker.SetLogger(loggerFactory);

            for (int i = 0; i < Greater.ColorsLength; i++)
                Greater.Great();

            ILogger logger = loggerFactory.CreateLogger(typeof(Program));

            if (debug)
            {
                logger.LogWarning("Дыбажым");
            }

            logger.LogInformation("Версия вора {version}", System.Reflection.Assembly.GetEntryAssembly()!.GetName().Version);

            if (File.Exists(configPath))
            {
                config = await Config.LoadAsync(configPath);
            }
            else
            {
                config = new Config(configPath);
                await config.SaveAsync();

                logger.LogInformation("Создали конфиг.");
            }

            if (config.Channel == null ||
                config.TwitchAPISecret == null || config.TwitchAPIClientId == null)
            {
                logger.LogError("Разберись с конфигом ебать");
                return;
            }

            twitchAPI = new TwitchAPI();
            twitchAPI.Settings.ClientId = config.TwitchAPIClientId;
            twitchAPI.Settings.Secret = config.TwitchAPISecret;

            if (config.ChannelId == null)
            {
                var callrsult = twitchAPI.Helix.Users.GetUsersAsync(logins: new List<string>() { config.Channel }).GetAwaiter().GetResult();

                if (callrsult.Users.Length == 0)
                {
                    logger.LogCritical($"Нет такого юзера");
                    return;
                }

                config.ChannelId = callrsult.Users[0].Id;

                await config.SaveAsync();

                logger.LogInformation($"Обновлён айди канала");
            }

            //q
            logger.LogInformation("Качество {resolution} {fps}", Program.config.PreferedVideoResolution, Program.config.PreferedVideoFps);

            //vk
            if (config.Vk != null)
            {
                logger.LogInformation("Вк добавлен");

                Upload.Kvk.VkUploader vk = new(Guid.Empty, loggerFactory, config.Vk);
                await vk.TestAsync();
            }
            else
            {
                logger.LogInformation("Без вк");
            }

            //do
            if (config.Ocean != null)
            {
                logger.LogInformation("ДО добавлен");

                var do_client = new DigitalOcean.API.DigitalOceanClient(config.Ocean.ApiToken);

                DigitalOcean.API.Models.Responses.Droplet droplet = await do_client.Droplets.Get(config.Ocean.DropletId);

                config.Ocean.Region = droplet.Region.Slug;

                logger.LogInformation("Регион дроплетов: {region}", config.Ocean.Region);
            }
            else
            {
                logger.LogInformation("Без ДО");
            }

            //timeweb
            if (config.Timeweb != null)
            {
                logger.LogInformation("Таймвеб добавлен");
                logger.LogInformation("Таймвеб таймаут {timeout}", config.Timeweb.RequestsTimeout);

                if (config.Timeweb.ValidateTokenOnStart)
                {
                    Space.TimeWeb.TimewebSpaceProvider timeWeb = new(Guid.Empty, loggerFactory, config.Timeweb);
                    await timeWeb.TestAsync();
                }
            }
            else
            {
                logger.LogInformation("Без таймвеба");
            }

            if (config.Conversion is ConversionConfig conversion)
            {
                logger.LogInformation("Конвертируем ({path})", conversion.FfmpegPath);

                ffmpeg = new Ffmpeg(config.Conversion, loggerFactory);

                if (!await ffmpeg.CheckAsync())
                {
                    return;
                }
            }
            else
            {
                logger.LogInformation("Без конверсии");
            }

            if (config.Downloader.SubCheck != null)
            {
                logger.LogInformation($"Чекаем сабгифтера");

                subChecker = new SubChecker(config.ChannelId, config.Downloader.SubCheck, loggerFactory);

                if (config.Downloader.SubCheck.CheckSubOnStart)
                {
                    await subChecker.GetSubAsync();
                }
            }

            if (config.Money != null)
            {
                var bill = new Bill(config.Money.Currency, config.Money.PerHourCost);

                logger.LogInformation("Стоимость приложения в час: {formatBill}", bill.Format());
            }

            statuser = new TwitchStatuser(loggerFactory);

            streamsManager = new(loggerFactory);

            if (!Directory.Exists(config.CacheDirectoryName))
            {
                Directory.CreateDirectory(config.CacheDirectoryName);
                logger.LogInformation("Создана папка для кеша.");
            }

            if (config.Email != null)
            {
                logger.LogInformation("Емейл добавлен.");

                emailer = new Emailer(config.Email, loggerFactory);
                if (!await emailer.ValidateAsync())
                {
                    return;
                }
            }

            statuser.Init();

            logger.LogInformation("ServerGC is {status}", System.Runtime.GCSettings.IsServerGC);

            while (true)
            {
                Greater.Great("debug; pubsub; finish; shutdown");

                string? line = Console.ReadLine();
                if (line == null)
                    continue;

                if (line == "debug")
                {
                    debug = !debug;

                    logger.LogInformation("дыбаг теперь {debug}", debug);
                }
                else if (line == "pubsub")
                {
                    if (statuser.pubsubChecker.debug_LastStreamEvent == null)
                    {
                        logger.LogInformation("{name} is null", nameof(statuser.pubsubChecker.debug_LastStreamEvent));
                        continue;
                    }

                    var passed = DateTime.UtcNow - statuser.pubsubChecker.debug_LastStreamEvent.Value;

                    logger.LogInformation("{date} - {passed}", statuser.pubsubChecker.debug_LastStreamEvent, passed);
                }
                else if (line == "finish")
                {
                    streamsManager.EndStream();
                    logger.LogInformation("ок");
                }
                else if (line == "shutdown")
                {
                    shutdown = true;
                    logger.LogInformation("ок");
                }
            }
        }
    }
}
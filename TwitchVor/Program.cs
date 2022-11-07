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

                    options.Colors = new List<ColoredConsoleOptions.ColoredCategory>()
                    {
                        new ColoredConsoleOptions.ColoredCategory()
                        {
                            Category =typeof(Greater).FullName,
                            FgColor = ConsoleColor.Black,
                            BgColor = ConsoleColor.White
                        }
                    };
                });
                builder.AddConsole(b => b.FormatterName = nameof(ColoredConsoleFormatter));

                if (debug)
                    builder.SetMinimumLevel(LogLevel.Debug);
            });

            Greater.Great(loggerFactory);

            ILogger logger = loggerFactory.CreateLogger(typeof(Program));

            DescriptionMaker.SetLogger(loggerFactory);

            if (debug)
            {
                logger.LogWarning("Дыбажым");
            }

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

            if (config.Conversion != null)
            {
                ffmpeg = new Ffmpeg(config.Conversion);
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

                if (config.Timeweb.ValidateTokenOnStart)
                {
                    using var api = new TimeWebApi();

                    api.SetAccessToken(config.Timeweb.AccessToken);

                    bool update = false;
                    try
                    {
                        await api.ListBucketsAsync();
                    }
                    catch (TimewebNet.Exceptions.BadCodeException badCodeE) when (badCodeE.Code == System.Net.HttpStatusCode.Forbidden)
                    {
                        update = true;

                        logger.LogWarning("Таймвеб форбиден.");
                    }

                    if ((config.Timeweb.AccessTokenExpirationDate - DateTimeOffset.UtcNow) < TimeSpan.FromDays(14))
                    {
                        update = true;
                    }

                    if (update)
                    {
                        logger.LogInformation("Обновляет токен таймвеба");

                        var auth = await api.GetTokenAsync(config.Timeweb.RefreshToken);

                        config.Timeweb.RefreshToken = auth.Refresh_token;
                        config.Timeweb.AccessToken = auth.Access_token;
                        config.Timeweb.AccessTokenExpirationDate = DateTimeOffset.UtcNow.AddSeconds(auth.Expires_in);

                        await config.SaveAsync();

                        logger.LogInformation("Обновили");
                    }
                }
            }
            else
            {
                logger.LogInformation("Без таймвеба");
            }

            if (config.Conversion is ConversionConfig conversion)
            {
                logger.LogInformation("Конвертируем ({path})", conversion.FfmpegPath);

                if (!File.Exists(conversion.FfmpegPath))
                {
                    logger.LogCritical("Не удаётся найти ффмпег");
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

            statuser = new TwitchStatuser(loggerFactory);

            streamsManager = new(loggerFactory);

            if (!Directory.Exists(config.CacheDirectoryName))
            {
                Directory.CreateDirectory(config.CacheDirectoryName);
                logger.LogInformation("Создана папка для кеша.");
            }

            if (config.Email != null)
            {
                emailer = new Emailer(config.Email, loggerFactory);
                if (await emailer.ValidateAsync())
                {
                    logger.LogInformation("Емейл в поряде");
                }
                else
                {
                    logger.LogCritical("Емейл каличный");
                    return;
                }
            }

            statuser.Init();

            while (true)
            {
                Console.WriteLine("Пошёл нахуй. debug pubsub finish shutdown");
                string? line = Console.ReadLine();
                if (line == null)
                {
                    continue;
                }

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
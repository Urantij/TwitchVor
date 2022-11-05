using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
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
            Greater.Great();

            debug = appArgs.Contains("--debug");
            if (debug)
            {
                ColorLog.LogWarning("Дыбажым");
            }

            if (File.Exists(configPath))
            {
                config = await Config.LoadAsync(configPath);
            }
            else
            {
                config = new Config(configPath);
                await config.SaveAsync();

                ColorLog.Log("Создали конфиг.");
            }

            if (config.Channel == null ||
                config.TwitchAPISecret == null || config.TwitchAPIClientId == null)
            {
                ColorLog.LogError("Разберись с конфигом ебать");
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
                    ColorLog.Log($"Нет такого юзера");
                    return;
                }

                config.ChannelId = callrsult.Users[0].Id;

                await config.SaveAsync();

                ColorLog.Log($"Обновлён айди канала");
            }

            //q
            ColorLog.Log($"Качество {Program.config.PreferedVideoQuality} {Program.config.PreferedVideoFps}");

            //vk
            if (config.Vk != null)
            {
                ColorLog.Log("Вк добавлен");
            }
            else
            {
                ColorLog.Log("Без вк");
            }

            if (config.Conversion is ConversionConfig conversion)
            {
                ColorLog.Log($"Конвертируем ({conversion.FfmpegPath})");

                if (!File.Exists(conversion.FfmpegPath))
                {
                    ColorLog.Log("Не удаётся найти ффмпег");
                    return;
                }
            }
            else
            {
                ColorLog.Log("Без конверсии");
            }

            if (config.Downloader.SubCheck != null)
            {
                ColorLog.Log($"Чекаем сабгифтера");

                subChecker = new SubChecker(config.ChannelId, config.Downloader.SubCheck);

                if (config.Downloader.SubCheck.CheckSubOnStart)
                {
                    await subChecker.GetSubAsync();
                }
            }

            statuser = new TwitchStatuser();

            streamsManager = new();

            if (!Directory.Exists(config.CacheDirectoryName))
            {
                Directory.CreateDirectory(config.CacheDirectoryName);
                ColorLog.Log("Создана папка для кеша.");
            }

            if (config.Email != null)
            {
                emailer = new Emailer(config.Email);
                if (await emailer.ValidateAsync())
                {
                    ColorLog.Log("Емейл в поряде");
                }
                else
                {
                    ColorLog.LogError("Емейл каличный");
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

                    ColorLog.Log($"дыбаг теперь {debug}");
                }
                else if (line == "pubsub")
                {
                    if (statuser.pubsubChecker.debug_LastStreamEvent == null)
                    {
                        ColorLog.Log($"{nameof(statuser.pubsubChecker.debug_LastStreamEvent)} is null");
                        continue;
                    }

                    var passed = DateTime.UtcNow - statuser.pubsubChecker.debug_LastStreamEvent.Value;

                    ColorLog.Log($"{statuser.pubsubChecker.debug_LastStreamEvent} - {passed}");
                }
                else if (line == "finish")
                {
                    streamsManager.EndStream();
                    ColorLog.Log("ок");
                }
                else if (line == "shutdown")
                {
                    shutdown = true;
                    ColorLog.Log("ок");
                }
            }
        }
    }
}
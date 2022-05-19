using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchVor.Configuration;
using TwitchVor.Finisher;
using TwitchVor.TubeYou;
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

        public static Emailer? emailer;

        public static bool debug = false;

        static void Main(string[] appArgs)
        {
            Great();

            debug = appArgs.Contains("--debug");
            if (debug)
            {
                ColorLog.LogWarning("Дыбажым");
            }

            if (File.Exists(configPath))
            {
                string configStr = File.ReadAllText(configPath);

                config = JsonConvert.DeserializeObject<Config>(configStr)!;
            }
            else
            {
                config = new Config();
                string configStr = JsonConvert.SerializeObject(config, Formatting.Indented);

                File.WriteAllText(configPath, configStr);
            }

            if (config.Channel == null || config.Channel == Config.emptyChannel ||
                config.TwitchAPISecret == null || config.TwitchAPIClientId == null)
            {
                ColorLog.LogError("Разберись с конфигом ебать");
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
                    ColorLog.Log($"Нет такого юзера");
                    return;
                }

                config.ChannelId = callrsult.Users[0].Id;

                File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

                ColorLog.Log($"Обновлён айди канала");
            }

            //q
            ColorLog.Log($"Качество {Program.config.PreferedVideoQuality} {Program.config.PreferedVideoFps}");

            //youtube
            if (config.YouTube != null)
            {
                ColorLog.Log("Ютуб добавлен");
            }
            else
            {
                ColorLog.Log("Без ютуба");
            }

            //do
            if (config.Ocean != null)
            {
                ColorLog.Log("ДО добавлен");

                var do_client = new DigitalOcean.API.DigitalOceanClient(config.Ocean.ApiToken);

                var droplet = do_client.Droplets.Get(config.Ocean.DropletId).GetAwaiter().GetResult();

                config.Ocean.Region = droplet.Region.Slug;

                ColorLog.Log($"Регион дроплетов: {config.Ocean.Region}");
            }
            else
            {
                ColorLog.Log("Без ДО");
            }

            if (config.Conversion is ConversionConfig conversion)
            {
                ColorLog.Log($"Конвертируем в {conversion.TargetFormat} с параметрами \"{conversion.Arguments}\" ({conversion.FfmpegPath})");

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

            if (config.Ocean != null && config.YouTube == null)
            {
                ColorLog.Log("Чего блять?");
                return;
            }

            if (config.Downloader?.SubCheck != null)
            {
                ColorLog.Log($"Чекаем сабгифтера");

                if (config.Downloader.SubCheck.CheckSubOnStart)
                {
                    SubChecker.GetSub(config.ChannelId!, config.Downloader.SubCheck.AppSecret, config.Downloader.SubCheck.AppClientId, config.Downloader.SubCheck.UserId, config.Downloader.SubCheck.RefreshToken).GetAwaiter().GetResult();
                }
            }

            statuser = new TwitchStatuser();

            streamsManager = new();

            if (!Directory.Exists(config.VideosDirectoryName))
            {
                Directory.CreateDirectory(config.VideosDirectoryName);
                ColorLog.Log("Создана папка для видео.");
            }

            if (!Directory.Exists(config.LocalDescriptionsDirectoryName))
            {
                Directory.CreateDirectory(config.LocalDescriptionsDirectoryName);
                ColorLog.Log("Создана папка для локальных описаний.");
            }

            if (config.Email != null)
            {
                emailer = new Emailer(config.Email);
                if (emailer.ValidateAsync().GetAwaiter().GetResult())
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
                Console.WriteLine("Пошёл нахуй. debug pubsub finish");
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
            }
        }

        static void Great()
        {
            ConsoleColor[] colors = new ConsoleColor[]
            {
                ConsoleColor.Red,
                ConsoleColor.DarkYellow,
                ConsoleColor.Yellow,
                ConsoleColor.Green,
                ConsoleColor.Blue,
                ConsoleColor.DarkBlue,
                ConsoleColor.Magenta,
            };

            foreach (var color in colors)
            {
                ColorLog.Log("Ты пидор.", null, color);
            }
        }
    }
}
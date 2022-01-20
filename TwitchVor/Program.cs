using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;
using TwitchLib.Api;
using TwitchVor.Configuration;
using TwitchVor.TubeYou;
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

        public static bool debug = false;

        static void Main(string[] appArgs)
        {
            Great();

            debug = appArgs.Contains("--debug");
            if (debug)
            {
                ColorLog.LogWarning("Debug is true");
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

            if (config.Channel == null ||
                config.TwitchAPISecret == null || config.TwitchAPIClientId == null)
            {
                ColorLog.LogError("Set Config");
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
                    ColorLog.Log($"No such user");
                    return;
                }

                config.ChannelId = callrsult.Users[0].Id;

                File.WriteAllText(configPath, JsonConvert.SerializeObject(config, Formatting.Indented));

                ColorLog.Log($"Updated channel id");
            }

            //youtube
            if (config.YouTube != null)
            {
                ColorLog.Log("Youtube added");
            }
            else
            {
                ColorLog.Log("No youtube");
            }

            statuser = new TwitchStatuser();

            streamsManager = new();

            if (!Directory.Exists(config.VideosDirectoryName))
            {
                Directory.CreateDirectory(config.VideosDirectoryName);
                ColorLog.Log("Created video directory.");
            }

            statuser.Init();

            while (true)
            {
                Console.WriteLine("Пошёл нахуй. debug pubsub");
                string? line = Console.ReadLine();
                if (line == null)
                {
                    continue;
                }

                if (line == "debug")
                {
                    debug = !debug;

                    ColorLog.Log($"Debug is {debug} now");
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
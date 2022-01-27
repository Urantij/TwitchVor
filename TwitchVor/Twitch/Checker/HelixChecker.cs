using System.Net.Sockets;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    class HelixChecker
    {
        private readonly TwitchStatuser statuser;

        private readonly Dictionary<string, string> gameIdToGameName = new();

        public event EventHandler<HelixCheck>? ChannelChecked;

        public HelixChecker(TwitchStatuser statuser)
        {
            this.statuser = statuser;
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "HelixChecker");
        }

        static void LogError(string message)
        {
            ColorLog.LogError(message, "HelixChecker");
        }

        public void Start()
        {
            StartCheckLoop();
        }

        private async void StartCheckLoop()
        {
            while (true)
            {
                var helixCheck = await CheckChannel();

                //если ошибка, стоит подождать чуть больше обычного
                if (helixCheck == null)
                {
                    await Task.Delay(Program.config.HelixCheckDelay.Multiply(1.5));
                    continue;
                }

                try
                {
                    if (helixCheck.check.online)
                        statuser.StreamUp(helixCheck.check, false);
                    else
                        statuser.StreamDown(helixCheck.check, false);

                    ChannelChecked?.Invoke(this, helixCheck);
                }
                catch (Exception e)
                {
                    LogError($"TwitchHelixChecker CheckLoop Exception\n{e}");
                }

                await Task.Delay(Program.config.HelixCheckDelay);
            }
        }

        //Должен быть способ умнее, но мне плохо
        /// <returns>null, если ошибка внеплановая</returns>
        private async Task<HelixCheck?> CheckChannel()
        {
            HelixCheck result;

            try
            {
                var response = await Program.twitchAPI.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { Program.config.ChannelId! }, first: 1);

                if (response.Streams.Length == 0)
                {
                    return new HelixCheck(new TwitchCheckInfo(false, DateTime.UtcNow))
                    {
                        info = null,
                    };
                }

                var stream = response.Streams[0];

                if (stream.Type.ToLower() != "live") return new HelixCheck(new TwitchCheckInfo(false, DateTime.UtcNow))
                {
                    info = null,
                };

                result = new HelixCheck(new TwitchCheckInfo(true, DateTime.UtcNow))
                {
                    info = new TwitchChannelInfo(stream.Title, stream.GameId, stream.ViewerCount)
                };

                if (gameIdToGameName.TryGetValue(result.info.gameId, out string? gameName))
                {
                    result.info.gameName = gameName;
                    return result;
                }
            }
            catch (TwitchLib.Api.Core.Exceptions.BadScopeException)
            {
                Log($"CheckChannel exception опять BadScopeException");

                return null;
            }
            catch (TwitchLib.Api.Core.Exceptions.InternalServerErrorException)
            {
                Log($"CheckChannel exception опять InternalServerErrorException");

                return null;
            }
            catch (HttpRequestException e) when (e.InnerException is IOException io)
            {
                LogError($"CheckChannel HttpRequestException.IOException: \"{io.Message}\"");

                return null;
            }
            catch (Exception e)
            {
                LogError($"CheckChannel exception\n{e}");

                return null;
            }

            try
            {
                var gameInfo = await Program.twitchAPI.Helix.Games.GetGamesAsync(gameIds: new List<string>()
                {
                    result.info.gameId
                });

                if (gameInfo.Games.Length > 0)
                {
                    result.info.gameName = gameInfo.Games[0].Name;

                    gameIdToGameName.Add(result.info.gameId, result.info.gameName);
                }
            }
            catch (Exception e)
            {
                LogError($"CheckChannel Game exception\n{e}");
            }

            return result;
        }
    }
}
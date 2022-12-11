using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    class HelixChecker : BaseChecker
    {
        public HelixChecker(ILoggerFactory loggerFactory)
            : base(loggerFactory)
        {
        }

        public void Start()
        {
            StartCheckLoop();
        }

        private async void StartCheckLoop()
        {
            while (true)
            {
                TwitchCheckInfo? checkInfo = await CheckChannelAsync();

                //если ошибка, стоит подождать чуть больше обычного
                if (checkInfo == null)
                {
                    await Task.Delay(Program.config.HelixCheckDelay.Multiply(1.5));
                    continue;
                }

                try
                {
                    OnChannelChecked(checkInfo);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "CheckLoop");
                }

                await Task.Delay(Program.config.HelixCheckDelay);
            }
        }

        /// <returns>null, если ошибка внеплановая</returns>
        private async Task<TwitchCheckInfo?> CheckChannelAsync()
        {
            TwitchLib.Api.Helix.Models.Streams.GetStreams.Stream stream;

            try
            {
                var response = await Program.twitchAPI.Helix.Streams.GetStreamsAsync(userIds: new List<string>() { Program.config.ChannelId! }, first: 1);

                if (response.Streams.Length == 0)
                {
                    return new TwitchCheckInfo(false, DateTime.UtcNow);
                }

                stream = response.Streams[0];

                if (!stream.Type.Equals("live", StringComparison.OrdinalIgnoreCase))
                    return new TwitchCheckInfo(false, DateTime.UtcNow);
            }
            catch (TwitchLib.Api.Core.Exceptions.BadScopeException)
            {
                _logger.LogWarning($"CheckChannel exception опять BadScopeException");

                return null;
            }
            catch (TwitchLib.Api.Core.Exceptions.InternalServerErrorException)
            {
                _logger.LogWarning($"CheckChannel exception опять InternalServerErrorException");

                return null;
            }
            catch (HttpRequestException e) when (e.InnerException is IOException io)
            {
                _logger.LogWarning("CheckChannel HttpRequestException.IOException: \"{Message}\"", io.Message);

                return null;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "CheckChannel");

                return null;
            }

            return new HelixCheck(true, DateTime.UtcNow, new TwitchChannelInfo(stream.Title, stream.GameName, stream.GameId, stream.ViewerCount));
        }
    }
}
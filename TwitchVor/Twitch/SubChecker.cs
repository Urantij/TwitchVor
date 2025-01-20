using Microsoft.Extensions.Logging;
using TwitchLib.Api;
using TwitchVor.Utility;

namespace TwitchVor.Twitch
{
    class SubCheck
    {
        public readonly bool sub;
        public readonly TwitchLib.Api.Helix.Models.Subscriptions.Subscription? subInfo;

        public SubCheck(bool sub, TwitchLib.Api.Helix.Models.Subscriptions.Subscription? subInfo)
        {
            this.sub = sub;
            this.subInfo = subInfo;
        }
    }

    class SubChecker
    {
        readonly ILogger _logger;

        readonly string channelId;
        readonly SubCheckConfig config;

        public SubChecker(string channelId, SubCheckConfig config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.channelId = channelId;
            this.config = config;
        }

        public async Task<SubCheck?> GetSubAsync()
        {
            try
            {
                TwitchAPI userApi = new();

                //всё равно нужно внутрь класть, или он ошибку кинет, что нужно класть
                userApi.Settings.ClientId = config.AppClientId;
                userApi.Settings.Secret = config.AppSecret;

                var token = await userApi.Auth.RefreshAuthTokenAsync(config.RefreshToken, config.AppSecret,
                    config.AppClientId);

                userApi.Settings.AccessToken = token.AccessToken;

                //https://github.com/TwitchLib/TwitchLib.Api/blob/816b6d46af4edb89f9f1f54d3344cd752a8f043f/TwitchLib.Api.Core/HttpCallHandlers/TwitchHttpClient.cs#L39
                //BadResourceException
                TwitchLib.Api.Helix.Models.Subscriptions.Subscription subscription;
                try
                {
                    var result =
                        await userApi.Helix.Subscriptions.CheckUserSubscriptionAsync(channelId, config.UserId,
                            token.AccessToken);
                    subscription = result.Data[0];
                }
                catch (TwitchLib.Api.Core.Exceptions.BadResourceException)
                {
                    _logger.LogInformation($"We have no sub");
                    return new SubCheck(false, null);
                }

                if (!subscription.IsGift)
                {
                    _logger.LogInformation("We have sub, but no subgifter");
                    return new SubCheck(true, null);
                }

                _logger.LogInformation("Our sub is {name} !", subscription.GifterName);

                return new SubCheck(true, subscription);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not fetch sub info.");
                return null;
            }
        }
    }
}
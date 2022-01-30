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

    static class SubChecker
    {
        static void Log(string message)
        {
            ColorLog.Log(message, "SubChecker");
        }

        public static async Task<SubCheck?> GetSub(string channelId, string appSecret, string appClientId, string userId, string refreshToken)
        {
            try
            {
                TwitchAPI userApi = new();

                //всё равно нужно внутрь класть, или он ошибку кинет, что нужно класть
                userApi.Settings.ClientId = appClientId;
                userApi.Settings.Secret = appSecret;

                var token = await userApi.Auth.RefreshAuthTokenAsync(refreshToken, appSecret, appClientId);

                userApi.Settings.AccessToken = token.AccessToken;

                //https://github.com/TwitchLib/TwitchLib.Api/blob/816b6d46af4edb89f9f1f54d3344cd752a8f043f/TwitchLib.Api.Core/HttpCallHandlers/TwitchHttpClient.cs#L39
                //BadResourceException
                TwitchLib.Api.Helix.Models.Subscriptions.Subscription subscription;
                try
                {
                    var result = await userApi.Helix.Subscriptions.CheckUserSubscriptionAsync(channelId, userId, token.AccessToken);
                    subscription = result.Data[0];
                }
                catch (TwitchLib.Api.Core.Exceptions.BadResourceException)
                {
                    Log($"We have no sub");
                    return new SubCheck(false, null);
                }
                
                if (!subscription.IsGift)
                {
                    Log("We have sub, but no subgifter");
                    return new SubCheck(true, null);
                }

                Log($"Our sub is {subscription.GifterName} !");

                return new SubCheck(true, subscription);
            }
            catch (Exception e)
            {
                Log($"Could not fetch sub info:\n{e}");
                return null;
            }
        }
    }
}
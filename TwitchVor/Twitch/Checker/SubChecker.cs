using TwitchLib.Api;
using TwitchVor.Utility;

namespace TwitchVor.Twitch.Checker
{
    static class SubChecker
    {
        static void Log(string message)
        {
            ColorLog.Log(message, "SubChecker");
        }

        public static async Task<string?> GetSub(string channelId, string appSecret, string appClientId, string userId, string refreshToken)
        {
            try
            {
                TwitchAPI userApi = new();

                //всё равно нужно внутрь класть, или он ошибку кинет, что нужно класть
                userApi.Settings.ClientId = appClientId;
                userApi.Settings.Secret = appSecret;

                var token = await userApi.Auth.RefreshAuthTokenAsync(refreshToken, appSecret, appClientId);

                userApi.Settings.AccessToken = token.AccessToken;

                var result = await userApi.Helix.Subscriptions.CheckUserSubscriptionAsync(channelId, userId, token.AccessToken);

                var data = result.Data[0];
                if (!data.IsGift)
                {
                    Log("We have no sub");
                    return null;
                }

                if (data.GifterName == null)
                {
                    Log("We have sub, but no subgifter");
                    return "???";
                }

                string subGifter;
                if (data.GifterName.Equals(data.GifterLogin, StringComparison.OrdinalIgnoreCase))
                {
                    subGifter = data.GifterName;
                }
                else
                {
                    subGifter = $"{data.GifterName} ({data.GifterLogin})";
                }

                Log($"Our sub is {subGifter} !");

                return subGifter;
            }
            catch (Exception e)
            {
                Log($"Could not fetch sub info:\n{e}");
                return null;
            }
        }
    }
}
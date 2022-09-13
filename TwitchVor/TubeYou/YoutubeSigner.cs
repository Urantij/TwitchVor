using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.TubeYou
{
    public static class YoutubeSigner
    {
        public static async Task<YoutubeCreds> GenerateCreds(string clientId, string secret)
        {
            Google.Apis.Auth.OAuth2.ClientSecrets secrets = new()
            {
                ClientId = clientId,
                ClientSecret = secret
            };

            var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20);
            var userId = System.Text.Encoding.ASCII.GetString(bytes);

            var scopes = new string[]
            {
                "https://www.googleapis.com/auth/youtube.download",
                "https://www.googleapis.com/auth/youtube.readonly",
                "https://www.googleapis.com/auth/youtube",
                "https://www.googleapis.com/auth/youtube.force-ssl",
                "https://www.googleapis.com/auth/youtubepartner",
                "https://www.googleapis.com/auth/youtubepartner-channel-audit",
                "https://www.googleapis.com/auth/youtube.upload",
                "https://www.googleapis.com/auth/youtube.channel-memberships.creator",
                "https://www.googleapis.com/auth/youtube.third-party-link.creator",
            };

            var userCredential = await Google.Apis.Auth.OAuth2.GoogleWebAuthorizationBroker.AuthorizeAsync(secrets, scopes, userId, CancellationToken.None);

            return new YoutubeCreds(userCredential.Token.RefreshToken, userCredential.UserId, clientId, secret);
        }
    }
}
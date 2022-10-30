using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VkNet;
using VkNet.Model;

namespace TwitchVor.Upload.Kvk
{
    public class VkUploader
    {
        readonly VkCreds creds;

        public VkUploader(VkCreds creds)
        {
            this.creds = creds;
        }

        public async Task<bool> UploadAsync(string name, string description, string fileName, FileStream fs)
        {
            VkApi api = new();
            await api.AuthorizeAsync(new ApiAuthParams()
            {
                ApplicationId = creds.ApplicationId,
                AccessToken = creds.ApiToken,
                Settings = VkNet.Enums.Filters.Settings.All
            });

            var saveResult = await api.Video.SaveAsync(new VkNet.Model.RequestParams.VideoSaveParams()
            {
                Name = name,
                Description = description,

                GroupId = creds.GroupId,
            });

            using HttpClient client = new();
            using MultipartFormDataContent content = new();
            using StreamContent streamContent = new(fs);

            content.Add(streamContent, "video_file", fileName);

            var response = await client.PostAsync(saveResult.UploadUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Console.WriteLine("Vk neudachno\n" + responseContent);
                return false;
            }

            return true;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VkNet;
using VkNet.Model;

namespace TwitchVor.Upload.Kvk
{
    class VkUploader : BaseUploader
    {
        readonly VkCreds creds;

        public override long SizeLimit => 256L * 1024L * 1024L * 1024L;

        public override TimeSpan DurationLimit => TimeSpan.MaxValue;

        public VkUploader(Guid guid, VkCreds creds)
            : base(guid)
        {
            this.creds = creds;
        }

        public override async Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content)
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
            using MultipartFormDataContent httpContent = new();
            using StreamContent streamContent = new(content);

            httpContent.Add(streamContent, "video_file", fileName);

            httpContent.Headers.ContentLength = size + 185;

            System.Console.WriteLine("начинает загрузку вк");

            var response = await client.PostAsync(saveResult.UploadUrl, httpContent);

            if (!response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                System.Console.WriteLine("Vk neudachno\n" + responseContent);
                return false;
            }

            System.Console.WriteLine("закончил загрузку вк");

            return true;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Upload.Kvk;

public class VkWaller
{
    readonly ILogger _logger;

    readonly VkCreds creds;
    readonly VkAppConfig appConfig;

    public VkWaller(ILoggerFactory loggerFactory, VkCreds creds)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        if (creds.WallRunner == null)
        {
            throw new NullReferenceException();
        }

        this.creds = creds;
        this.appConfig = creds.WallRunner;
    }

    public async Task MakePostAsync(IEnumerable<long> videoIds)
    {
        using VkNet.VkApi vkApi = new();
        await vkApi.AuthorizeAsync(new VkNet.Model.ApiAuthParams()
        {
            ApplicationId = appConfig.ApplicationId,
            AccessToken = appConfig.ApiToken,
            Settings = VkNet.Enums.Filters.Settings.Wall
        });

        await vkApi.Wall.PostAsync(new VkNet.Model.WallPostParams()
        {
            Guid = Guid.NewGuid().ToString(),

            Attachments = videoIds.Select(id => new VkNet.Model.Video()
            {
                OwnerId = -creds.GroupId,
                Type = "video",
                Id = id
            }).ToArray(),

            OwnerId = -creds.GroupId,
            FromGroup = true,
            Signed = false,
        });

        _logger.LogInformation("Запостили кринж.");
    }
}

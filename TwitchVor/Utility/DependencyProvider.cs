using Microsoft.Extensions.Logging;
using TwitchVor.Space;
using TwitchVor.Space.Local;
using TwitchVor.Space.TimeWeb;
using TwitchVor.Upload;
using TwitchVor.Upload.FileSystem;
using TwitchVor.Upload.Kvk;
using TwitchVor.Upload.TubeYou;

namespace TwitchVor.Utility;

internal static class DependencyProvider
{
    public static BaseSpaceProvider GetSpaceProvider(Guid guid, ILoggerFactory loggerFactory)
    {
        if (Program.config.Timeweb != null)
        {
            return new TimewebSpaceProvider(guid, loggerFactory, Program.config.Timeweb);
        }

        return new LocalSpaceProvider(guid, loggerFactory, MakeLocalSpacePath(guid, false));
    }

    public static List<BaseUploader> GetAllUploaders(Guid guid, ILoggerFactory loggerFactory)
    {
        List<BaseUploader> list = new();

        if (Program.config.Vk != null)
        {
            list.Add(new VkUploader(guid, loggerFactory, Program.config.Vk));
        }

        if (Program.config.Youtube != null)
        {
            list.Add(new YoutubeUploader(guid, loggerFactory, Program.config.Youtube));
        }

        list.Add(new FileUploader(guid, loggerFactory,
            Path.ChangeExtension(MakeLocalSpacePath(guid, false), "video")));

        return list;
    }

    public static List<BaseUploader> GetUploaders(Guid guid, ILoggerFactory loggerFactory)
    {
        List<BaseUploader> list = new();

        if (Program.config.Vk != null)
        {
            list.Add(new VkUploader(guid, loggerFactory, Program.config.Vk));
        }

        if (Program.config.Youtube != null)
        {
            list.Add(new YoutubeUploader(guid, loggerFactory, Program.config.Youtube));
        }

        if (list.Count == 0)
        {
            list.Add(new FileUploader(guid, loggerFactory,
                Path.ChangeExtension(MakeLocalSpacePath(guid, false), "video")));
        }

        return list;
    }

    public static string MakePath(string fileName)
    {
        return Path.Combine(Program.config.CacheDirectoryName, fileName);
    }

    public static string MakeLocalSpacePath(Guid guid, bool temp)
    {
        string fileName;
        if (temp)
        {
            fileName = "temp" + guid.ToString("N") + ".ts";
        }
        else
        {
            fileName = guid.ToString("N") + ".ts";
        }

        return Path.Combine(Program.config.CacheDirectoryName, fileName);
    }
}
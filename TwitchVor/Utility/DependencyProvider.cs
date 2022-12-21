using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Space;
using TwitchVor.Space.Local;
using TwitchVor.Space.TimeWeb;
using TwitchVor.Upload;
using TwitchVor.Upload.FileSystem;
using TwitchVor.Upload.Kvk;

namespace TwitchVor.Utility
{
    static class DependencyProvider
    {
        public static BaseSpaceProvider GetSpaceProvider(Guid guid, ILoggerFactory loggerFactory)
        {
            if (Program.config.Timeweb != null)
            {
                return new TimewebSpaceProvider(guid, loggerFactory, Program.config.Timeweb);
            }

            return new LocalSpaceProvider(guid, loggerFactory, MakeLocalSpacePath(guid, false));
        }

        public static BaseUploader GetUploader(Guid guid, ILoggerFactory loggerFactory)
        {
            if (Program.config.Vk != null)
            {
                return new VkUploader(guid, loggerFactory, Program.config.Vk);
            }

            return new FileUploader(guid, loggerFactory, Path.ChangeExtension(MakeLocalSpacePath(guid, false), "video"));
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
}
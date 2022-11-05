using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Space;
using TwitchVor.Space.Local;
using TwitchVor.Space.OceanDigital;
using TwitchVor.Space.TimeWeb;
using TwitchVor.Upload;
using TwitchVor.Upload.FileSystem;
using TwitchVor.Upload.Kvk;

namespace TwitchVor.Utility
{
    static class DependencyProvider
    {
        public static BaseSpaceProvider GetSpaceProvider(Guid guid)
        {
            if (Program.config.Timeweb != null)
            {
                return new TimewebSpaceProvider(guid, Program.config.Timeweb);
            }
            else if (Program.config.Ocean != null)
            {
                return new DigitalOceanSpaceProvider(guid, Program.config.Ocean);
            }

            return new LocalSpaceProvider(guid, MakeLocalSpacePath(guid, false));
        }

        public static BaseUploader GetUploader(Guid guid)
        {
            if (Program.config.Vk != null)
            {
                return new VkUploader(guid, Program.config.Vk);
            }

            return new FileUploader(guid, Path.ChangeExtension(MakeLocalSpacePath(guid, false), "video"));
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
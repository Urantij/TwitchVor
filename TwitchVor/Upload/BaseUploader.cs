using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Vvideo;

namespace TwitchVor.Upload
{
    abstract class BaseUploader
    {
        protected readonly ILogger _logger;

        protected Guid guid;

        /// <summary>
        /// В байтах
        /// </summary>
        public abstract long SizeLimit { get; }

        public abstract TimeSpan DurationLimit { get; }

        protected BaseUploader(Guid guid, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.guid = guid;
        }

        public abstract Task<bool> UploadAsync(string name, string description, string fileName, long size, Stream content);
    }
}
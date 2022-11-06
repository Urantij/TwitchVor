using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Space
{
    public abstract class BaseSpaceProvider
    {
        protected readonly ILogger _logger;

        protected readonly Guid guid;

        public bool Ready { get; protected set; }

        /// <summary>
        /// Можно ли писать несколько сегментов сразу.
        /// То есть этот спейс держит сегменты раздельно, учитывая их id.
        /// В ином случае он кидает их в один файл. Зато по порядку.
        /// </summary>
        public abstract bool AsyncUpload { get; }

        protected BaseSpaceProvider(Guid guid, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.guid = guid;
        }

        public abstract Task InitAsync();

        public abstract Task PutDataAsync(int id, Stream contentStream, long length);

        public abstract Task ReadDataAsync(int id, long offset, long length, Stream inputStream);

        public abstract Task CloseAsync();

        public abstract Task DestroyAsync();
    }
}
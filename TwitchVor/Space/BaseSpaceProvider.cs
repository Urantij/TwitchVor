using Microsoft.Extensions.Logging;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Space
{
    public abstract class BaseSpaceProvider
    {
        protected readonly ILogger _logger;

        protected readonly Guid guid;

        public bool Ready { get; protected set; }

        public IPricer? pricer;

        protected BaseSpaceProvider(Guid guid, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.guid = guid;
        }

        public abstract Task InitAsync();

        public abstract Task PutDataAsync(int id, Stream contentStream, long length,
            CancellationToken cancellationToken = default);

        public abstract Task ReadAllDataAsync(Stream inputStream, long length, long offset,
            CancellationToken cancellationToken = default);

        public abstract Task CloseAsync();

        public abstract Task DestroyAsync();
    }
}
using DigitalOcean.API;
using Microsoft.Extensions.Logging;
using TwitchVor.Utility;

namespace TwitchVor.Space.OceanDigital
{
    /// <summary>
    /// Пульт от удаления дроплета
    /// </summary>
    class DigitalOceanVolumeOperator
    {
        readonly ILogger _logger;

        readonly DigitalOceanClient client;

        readonly long dropletId;
        readonly string volumeId;
        readonly string region;

        public readonly string volumeName;

        public DigitalOceanVolumeOperator(DigitalOceanClient client, long dropletId, string volumeId, string region, string volumeName, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.client = client;
            this.dropletId = dropletId;
            this.volumeId = volumeId;
            this.region = region;
            this.volumeName = volumeName;
        }

        public async Task DetachAsync()
        {
            _logger.LogInformation("Detaching {volumeId}...", volumeId);
            await client.VolumeActions.Detach(volumeId, dropletId, region);
            _logger.LogInformation("Detached {volumeId}.", volumeId);
        }

        public async Task DeleteAsync()
        {
            _logger.LogInformation("Deleting {volumeId}...", volumeId);
            await client.Volumes.Delete(volumeId);
            _logger.LogInformation("Deleted {volumeId}.", volumeId);
        }

        public string GetVolumePath()
        {
            return DigitalOceanVolumeCreator.GetVolumePath(volumeName);
        }
    }
}
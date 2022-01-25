using DigitalOcean.API;
using TwitchVor.Utility;

namespace TwitchVor.Ocean
{
    /// <summary>
    /// Пульт от удаления дроплета
    /// </summary>
    class DigitalOceanVolumeOperator
    {
        readonly DigitalOceanClient client;

        readonly long dropletId;
        readonly string volumeId;
        readonly string region;

        public readonly string volumeName;

        public DigitalOceanVolumeOperator(DigitalOceanClient client, long dropletId, string volumeId, string region, string volumeName)
        {
            this.client = client;
            this.dropletId = dropletId;
            this.volumeId = volumeId;
            this.region = region;
            this.volumeName = volumeName;
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "OceanOperator", ConsoleColor.Blue);
        }

        public async Task DeleteAsync()
        {
            Log("Detaching...");
            await client.VolumeActions.Detach(volumeId, dropletId, region);
            Log("Deleting...");
            await client.Volumes.Delete(volumeId);
        }
    }
}
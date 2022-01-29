using DigitalOcean.API;
using TwitchVor.Utility;

namespace TwitchVor.Ocean
{
    /// <summary>
    /// Создаёт пульт от дроплета.
    /// Разделил, тому шо не хотелось ебаться с нуллабл полями
    /// </summary>
    class DigitalOceanVolumeCreator
    {
        readonly OceanCreds oceanCreds;

        readonly DigitalOceanClient client;

        public readonly string volumeName;
        public readonly int dropletSizeGB;

        public DigitalOceanVolumeCreator(OceanCreds oceanCreds, string volumeName, int dropletSizeGB)
        {
            this.oceanCreds = oceanCreds;
            this.volumeName = volumeName;
            this.dropletSizeGB = dropletSizeGB;

            client = new DigitalOceanClient(oceanCreds.ApiToken);
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "OceanCreator", ConsoleColor.Blue);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception">Если не удалось атач сделать</exception>
        public async Task<DigitalOceanVolumeOperator> CreateAsync()
        {
            DigitalOcean.API.Models.Requests.Volume requestVolume = new()
            {
                Name = volumeName,
                Description = $"Created by vorishka {DateTime.Now:HH:mm:ss dd.MM}",
                SizeGigabytes = dropletSizeGB,
                FilesystemType = "ext4",
                Region = oceanCreds.Region,
            };

            var responseVolume = await client.Volumes.Create(requestVolume);
            string volumeId = responseVolume.Id;

            Log($"Created volume {volumeId}");

            var attachAction = await client.VolumeActions.Attach(responseVolume.Id, oceanCreds.DropletId, oceanCreds.Region);
            var attachActionId = attachAction.Id;

            Log($"Attaching... {attachAction.Id}");

            //TODO получше
            while (true)
            {
                if (attachAction.Status == "completed")
                {
                    break;
                }
                else if (attachAction.Status == "in-progress")
                {
                    Log("Progress...");
                    await Task.Delay(5000);

                    attachAction = await CheckActionAsync(volumeId, attachActionId);
                }
                else
                {
                    var ex = new Exception($"Attach error: \"{attachAction.Status}\"");
                    throw ex;
                }
            }

            Log($"Attached!");

            return new DigitalOceanVolumeOperator(client, oceanCreds.DropletId, volumeId, oceanCreds.Region, volumeName);
        }

        async Task<DigitalOcean.API.Models.Responses.Action> CheckActionAsync(string volumeId, long actionId)
        {
            return await client.VolumeActions.GetAction(volumeId, actionId);
        }

        public static string GenerateVolumeName(DateTime date)
        {
            //Must be lowercase and be composed only of numbers, letters and "-", up to a limit of 64 characters.
            return $"temp{date:MM}{date:dd}{date:HH}{date:mm}{date:ss}";
        }
    }
}
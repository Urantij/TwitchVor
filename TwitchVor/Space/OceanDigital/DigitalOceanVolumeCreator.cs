using DigitalOcean.API;
using Microsoft.Extensions.Logging;
using TwitchVor.Utility;

namespace TwitchVor.Space.OceanDigital
{
    /// <summary>
    /// Создаёт пульт от дроплета.
    /// Разделил, тому шо не хотелось ебаться с нуллабл полями
    /// </summary>
    class DigitalOceanVolumeCreator
    {
        readonly ILogger _logger;
        readonly ILoggerFactory loggerFactory;

        readonly OceanCreds oceanCreds;

        readonly DigitalOceanClient client;

        public readonly string volumeName;
        public readonly int dropletSizeGB;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oceanCreds"></param>
        /// <param name="volumeName">Must be lowercase and be composed only of numbers, letters and "-", up to a limit of 64 characters.</param>
        /// <param name="dropletSizeGB"></param>
        public DigitalOceanVolumeCreator(OceanCreds oceanCreds, string volumeName, int dropletSizeGB, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());
            this.loggerFactory = loggerFactory;

            this.oceanCreds = oceanCreds;
            this.volumeName = volumeName;
            this.dropletSizeGB = dropletSizeGB;

            client = new DigitalOceanClient(oceanCreds.ApiToken);
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

            DigitalOcean.API.Models.Responses.Volume? responseVolume = await client.Volumes.Create(requestVolume);

            string volumeId = responseVolume.Id;

            _logger.LogInformation("Created volume {volumeId}", volumeId);

            DigitalOcean.API.Models.Responses.Action? attachAction = null;

            while (attachAction == null)
            {
                bool sentEmail = false;

                try
                {
                    attachAction = await client.VolumeActions.Attach(responseVolume.Id, oceanCreds.DropletId, oceanCreds.Region);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception on volume attachment.");

                    if (CheckVolumeExtremeCreation())
                    {
                        goto end;
                    }
                    else
                    {
                        _logger.LogCritical("To continue, create {path} file", GetExtremePath());
                    }

                    if (Program.emailer != null && !sentEmail)
                    {
                        await Program.emailer.SendCriticalErrorAsync("Исключение при присоединении места.");

                        sentEmail = true;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
            }

            var attachActionId = attachAction.Id;

            _logger.LogInformation("Attaching... {attachActionId}", attachAction.Id);

            //TODO получше
            while (true)
            {
                if (attachAction.Status == "completed")
                {
                    break;
                }
                else if (attachAction.Status == "in-progress")
                {
                    _logger.LogInformation("Progress...");
                    await Task.Delay(5000);

                    attachAction = await client.VolumeActions.GetAction(volumeId, attachActionId);
                }
                else
                {
                    throw new Exception($"Attach error: \"{attachAction.Status}\"");
                }
            }

        end:;

            _logger.LogInformation($"Attached!");

            return new DigitalOceanVolumeOperator(client, oceanCreds.DropletId, volumeId, oceanCreds.Region, volumeName, loggerFactory);
        }

        bool CheckVolumeExtremeCreation()
        {
            return File.Exists(GetExtremePath());
        }

        string GetExtremePath()
        {
            return Path.Combine(GetVolumePath(), "ok");
        }

        public string GetVolumePath()
        {
            return GetVolumePath(volumeName);
        }

        public static string GetVolumePath(string volumeName)
        {
            return Path.Combine("/mnt", volumeName);
        }
    }
}
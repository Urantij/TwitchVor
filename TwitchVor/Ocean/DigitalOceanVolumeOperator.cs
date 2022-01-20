using DigitalOcean.API;
using TwitchVor.Utility;

namespace TwitchVor.Ocean
{
    /// <summary>
    /// Создаёт и следит за объёмищем
    /// </summary>
    class DigitalOceanVolumeOperator
    {
        readonly OceanCreds oceanCreds;

        readonly DigitalOceanClient client;

        string? volumeId;

        public readonly string volumeName;

        TaskCompletionSource? creationSource;
        public Task? GetCreationTask => creationSource?.Task;

        public bool Ready => GetCreationTask?.IsCompleted == true; //не комплитедсасесфули потому что я хочу видеть мир в огне

        public DigitalOceanVolumeOperator(OceanCreds oceanCreds, string volumeName)
        {
            this.oceanCreds = oceanCreds;
            this.volumeName = volumeName;

            client = new DigitalOceanClient(oceanCreds.ApiToken);
        }

        static void Log(string message)
        {
            ColorLog.Log(message, "Ocean", ConsoleColor.Blue);
        }

        public async Task CreateAsync()
        {
            creationSource = new();

            DigitalOcean.API.Models.Requests.Volume requestVolume = new()
            {
                Name = volumeName,
                Description = $"Created by vorishka {DateTime.Now:HH:mm:ss dd.MM}",
                SizeGigabytes = oceanCreds.SizeGigabytes,
                FilesystemType = "ext4",
                Region = oceanCreds.Region,
            };

            var responseVolume = await client.Volumes.Create(requestVolume);
            volumeId = responseVolume.Id;

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
                    creationSource.SetException(ex);
                    throw ex;
                }
            }

            Log($"Attached!");
            creationSource.SetResult();
        }

        async Task<DigitalOcean.API.Models.Responses.Action> CheckActionAsync(string volumeId, long actionId)
        {
            return await client.VolumeActions.GetAction(volumeId, actionId);
        }

        public async Task DeleteAsync()
        {
            await client.Volumes.Delete(volumeId);
        }

        public static string GenerateVolumeName(DateTime date)
        {
            //Must be lowercase and be composed only of numbers, letters and "-", up to a limit of 64 characters.
            return $"temp{date:MM}{date:dd}{date:HH}{date:mm}{date:ss}";
        }
    }
}
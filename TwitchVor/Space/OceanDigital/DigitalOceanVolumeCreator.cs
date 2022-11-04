// using DigitalOcean.API;
// using TwitchVor.Utility;

// namespace TwitchVor.Space.OceanDigital
// {
//     /// <summary>
//     /// Создаёт пульт от дроплета.
//     /// Разделил, тому шо не хотелось ебаться с нуллабл полями
//     /// </summary>
//     class DigitalOceanVolumeCreator
//     {
//         readonly OceanCreds oceanCreds;

//         readonly DigitalOceanClient client;

//         public readonly string volumeName;
//         public readonly int dropletSizeGB;

//         public DigitalOceanVolumeCreator(OceanCreds oceanCreds, string volumeName, int dropletSizeGB)
//         {
//             this.oceanCreds = oceanCreds;
//             this.volumeName = volumeName;
//             this.dropletSizeGB = dropletSizeGB;

//             client = new DigitalOceanClient(oceanCreds.ApiToken);
//         }

//         static void Log(string message)
//         {
//             ColorLog.Log(message, "OceanCreator", ConsoleColor.Blue);
//         }

//         static void LogError(string message)
//         {
//             ColorLog.LogError(message, "OceanCreator");
//         }

//         /// <summary>
//         /// 
//         /// </summary>
//         /// <returns></returns>
//         /// <exception cref="Exception">Если не удалось атач сделать</exception>
//         public async Task<DigitalOceanVolumeOperator> CreateAsync()
//         {
//             DigitalOcean.API.Models.Requests.Volume requestVolume = new()
//             {
//                 Name = volumeName,
//                 Description = $"Created by vorishka {DateTime.Now:HH:mm:ss dd.MM}",
//                 SizeGigabytes = dropletSizeGB,
//                 FilesystemType = "ext4",
//                 Region = oceanCreds.Region,
//             };

//             DigitalOcean.API.Models.Responses.Volume? responseVolume = null;
//             while (responseVolume == null)
//             {
//                 bool sentEmail = false;

//                 try
//                 {
//                     responseVolume = await client.Volumes.Create(requestVolume);
//                 }
//                 catch (Exception e)
//                 {
//                     LogError($"Exception on volume creation.\n{e}");

//                     if (Program.emailer != null && !sentEmail)
//                     {
//                         await Program.emailer.SendCriticalErrorAsync("Исключение при создании места.");

//                         sentEmail = true;
//                     }

//                     await Task.Delay(TimeSpan.FromSeconds(10));
//                 }
//             }

//             string volumeId = responseVolume.Id;

//             Log($"Created volume {volumeId}");

//             DigitalOcean.API.Models.Responses.Action? attachAction = null;

//             while (attachAction == null)
//             {
//                 bool sentEmail = false;

//                 try
//                 {
//                     attachAction = await client.VolumeActions.Attach(responseVolume.Id, oceanCreds.DropletId, oceanCreds.Region);
//                 }
//                 catch (Exception e)
//                 {
//                     LogError($"Exception on volume attachment.\n{e}");

//                     if (CheckVolumeExtremeCreation())
//                     {
//                         goto end;
//                     }
//                     else
//                     {
//                         Log($"To continue, create /mnt/{volumeName}/ok file");
//                     }

//                     if (Program.emailer != null && !sentEmail)
//                     {
//                         await Program.emailer.SendCriticalErrorAsync("Исключение при присоединении места.");

//                         sentEmail = true;
//                     }

//                     await Task.Delay(TimeSpan.FromSeconds(10));
//                 }
//             }

//             var attachActionId = attachAction.Id;

//             Log($"Attaching... {attachAction.Id}");

//             //TODO получше
//             while (true)
//             {
//                 if (attachAction.Status == "completed")
//                 {
//                     break;
//                 }
//                 else if (attachAction.Status == "in-progress")
//                 {
//                     Log("Progress...");
//                     await Task.Delay(5000);

//                     attachAction = await client.VolumeActions.GetAction(volumeId, attachActionId);
//                 }
//                 else
//                 {
//                     var ex = new Exception($"Attach error: \"{attachAction.Status}\"");
//                     throw ex;
//                 }
//             }

//             end:;

//             Log($"Attached!");

//             return new DigitalOceanVolumeOperator(client, oceanCreds.DropletId, volumeId, oceanCreds.Region, volumeName);
//         }

//         bool CheckVolumeExtremeCreation()
//         {
//             return File.Exists($"/mnt/{volumeName}/ok");
//         }

//         public static string GenerateVolumeName(DateTime date)
//         {
//             //Must be lowercase and be composed only of numbers, letters and "-", up to a limit of 64 characters.
//             return $"temp{date:MM}{date:dd}{date:HH}{date:mm}{date:ss}";
//         }
//     }
// }
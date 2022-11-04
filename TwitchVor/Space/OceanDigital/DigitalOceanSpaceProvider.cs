// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Threading.Tasks;

// namespace TwitchVor.Space.OceanDigital
// {
//     public class DigitalOceanSpaceProvider : BaseSpaceProvider
//     {
//         public Task InitAsync()
//         {
//             throw new NotImplementedException();
//         }

//         public Task PutDataAsync(Stream contentStream)
//         {
//             throw new NotImplementedException();
//         }

//         public Task CloseAsync()
//         {
//             throw new NotImplementedException();
//         }

//         async Task<bool> DestroyVolumes(List<DigitalOceanVolumeOperator> operators)
//         {
//             //расправа

//             List<Task<bool>> tasks = new();
//             foreach (var op in operators)
//             {
//                 var task = Task.Run<bool>(async () =>
//                 {
//                     bool fine = false;

//                     int retries = 0;
//                     while (retries < 3)
//                     {
//                         try
//                         {
//                             await op.DetachAsync();
//                             break;
//                         }
//                         catch (Exception e)
//                         {
//                             Log($"Не удалось отсоединить вольюм {op.volumeName}. Исключение:\n{e}");

//                             retries++;
//                             await Task.Delay(TimeSpan.FromSeconds(5));
//                         }
//                     }

//                     await Task.Delay(TimeSpan.FromSeconds(5));

//                     retries = 0;
//                     while (retries < 3)
//                     {
//                         try
//                         {
//                             await op.DeleteAsync();

//                             fine = true;
//                             break;
//                         }
//                         catch (Exception e)
//                         {
//                             Log($"Не удалось удалить вольюм {op.volumeName}. Исключение:\n{e}");

//                             retries++;
//                             await Task.Delay(TimeSpan.FromSeconds(5));
//                         }
//                     }

//                     await Task.Delay(TimeSpan.FromSeconds(5));

//                     //хз че будет, если не удалённому вольюму удалить папку, но мне похуй
//                     try
//                     {
//                         Directory.Delete($"/mnt/{op.volumeName}");
//                         Log($"Папка {op.volumeName} удалена");
//                     }
//                     catch (Exception e)
//                     {
//                         Log($"Не удалось удалить папку {op.volumeName}: {e.Message}");
//                     }

//                     await Task.Delay(TimeSpan.FromSeconds(5));

//                     return fine;
//                 });

//                 tasks.Add(task);

//                 await Task.Delay(TimeSpan.FromSeconds(1));
//             }

//             Task.WaitAll(tasks.ToArray());

//             return tasks.All(t => t.Result);
//         }
//     }
// }
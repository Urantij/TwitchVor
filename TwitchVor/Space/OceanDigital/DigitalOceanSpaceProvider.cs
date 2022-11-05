using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Space.Local;
using TwitchVor.Utility;

namespace TwitchVor.Space.OceanDigital
{
    class DigitalOceanSpaceProvider : BaseSpaceProvider
    {
        readonly OceanCreds creds;

        DigitalOceanVolumeOperator? volumeOperator;

        LocalSpaceProvider? localSpaceProvider;

        public override bool AsyncUpload => false;

        public DigitalOceanSpaceProvider(Guid guid, OceanCreds creds)
            : base(guid)
        {
            this.creds = creds;
        }

        public override async Task InitAsync()
        {
            DigitalOceanVolumeCreator volumeCreator = new(creds, guid.ToString("N")[..64].ToLower(), creds.SizeGigabytes);

            volumeOperator = await volumeCreator.CreateAsync();

            var path = Path.Combine(volumeCreator.GetVolumePath(), guid.ToString("N") + ".ts");

            localSpaceProvider = new LocalSpaceProvider(guid, path);
            await localSpaceProvider.InitAsync();

            Ready = true;
        }

        public override async Task PutDataAsync(int id, Stream contentStream, long length)
        {
            if (localSpaceProvider == null)
                throw new NullReferenceException($"{nameof(localSpaceProvider)} is null");

            await localSpaceProvider.PutDataAsync(id, contentStream, length);
        }

        public override async Task ReadDataAsync(int id, long offset, long length, Stream inputStream)
        {
            if (localSpaceProvider == null)
                throw new NullReferenceException($"{nameof(localSpaceProvider)} is null");

            await localSpaceProvider.ReadDataAsync(id, offset, length, inputStream);
        }

        public override async Task CloseAsync()
        {
            if (localSpaceProvider != null)
            {
                await localSpaceProvider.CloseAsync();
            }
        }

        public override async Task DestroyAsync()
        {
            if (localSpaceProvider != null)
            {
                await localSpaceProvider.DestroyAsync();
            }

            if (volumeOperator == null)
                return;

            await volumeOperator.DetachAsync();

            await Task.Delay(TimeSpan.FromSeconds(5));

            {
                bool fine = false;

                int retries = 0;
                while (retries < 3)
                {
                    try
                    {
                        await volumeOperator.DeleteAsync();
                        fine = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        ColorLog.LogError($"Не удалось удалить вольюм {volumeOperator.volumeName}. Исключение:\n{e}", nameof(DigitalOceanSpaceProvider));

                        retries++;
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }

                if (!fine)
                    throw new Exception("Не удалось удалить вольюм");
            }

            Directory.Delete(volumeOperator.GetVolumePath());
            ColorLog.LogError($"Всё удалено.", nameof(DigitalOceanSpaceProvider));
        }
    }
}
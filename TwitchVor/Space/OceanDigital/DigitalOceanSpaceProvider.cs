using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Space.Local;
using TwitchVor.Utility;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Space.OceanDigital
{
    class DigitalOceanSpaceProvider : BaseSpaceProvider
    {
        const decimal taxesMult = 1.2M;
        const decimal volumeCostPerGBPerHour = 0.00015M;

        readonly ILoggerFactory _loggerFactory;

        readonly OceanCreds creds;

        DigitalOceanVolumeOperator? volumeOperator;

        LocalSpaceProvider? localSpaceProvider;

        public override bool AsyncUpload => false;

        public DigitalOceanSpaceProvider(Guid guid, ILoggerFactory loggerFactory, OceanCreds creds)
            : base(guid, loggerFactory)
        {
            _loggerFactory = loggerFactory;
            this.creds = creds;
        }

        public override async Task InitAsync()
        {
            DigitalOceanVolumeCreator volumeCreator = new(creds, guid.ToString("N")[..64].ToLower(), creds.SizeGigabytes, _loggerFactory);

            volumeOperator = await volumeCreator.CreateAsync();

            pricer = new TimeBasedPricer(DateTimeOffset.UtcNow, new Bill(Currency.USD, volumeCostPerGBPerHour * volumeCreator.dropletSizeGB * taxesMult));

            var path = Path.Combine(volumeCreator.GetVolumePath(), guid.ToString("N") + ".ts");

            localSpaceProvider = new LocalSpaceProvider(guid, _loggerFactory, path);
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
                        _logger.LogError(e, "Не удалось удалить вольюм {volumeName}.", volumeOperator.volumeName);

                        retries++;
                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }
                }

                if (!fine)
                    throw new Exception("Не удалось удалить вольюм");
            }

            Directory.Delete(volumeOperator.GetVolumePath());

            _logger.LogInformation("Всё удалено.");
        }
    }
}
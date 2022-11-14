using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Minio;
using TimewebNet.Models;
using TimeWebNet;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Space.TimeWeb
{
    public class TimewebSpaceProvider : BaseSpaceProvider
    {
        const decimal perHourCost = 349M / 30M / 24M;
        const S3ServiceType s3Type = S3ServiceType.Lite;

        private const string endpoint = "s3.timeweb.com";

        readonly TimewebConfig config;

        readonly TimeWebApi api;

        ListBucketsResponseModel.StorageModel? bucket;
        MinioClient? s3Client;

        public override bool Stable => false;
        public override bool AsyncUpload => true;

        public TimewebSpaceProvider(Guid guid, ILoggerFactory loggerFactory, TimewebConfig config)
            : base(guid, loggerFactory)
        {
            this.config = config;

            this.api = new TimeWebApi();
        }

        /// <summary>
        /// Делает запрос с RefreshToken, пишет ответ в конфиг.
        /// Кидает ошибки.
        /// </summary>
        /// <returns></returns>
        async Task UpdateTokenAsync()
        {
            _logger.LogInformation("Обновляем токен...");

            AuthResponseModel auth = await api.GetTokenAsync(config.RefreshToken);

            config.RefreshToken = auth.Refresh_token;
            config.AccessToken = auth.Access_token;
            config.AccessTokenExpirationDate = DateTimeOffset.UtcNow.AddSeconds(auth.Expires_in);

            await Program.config.SaveAsync();

            _logger.LogInformation("Токен обновлён.");
        }

        public async Task TestAsync()
        {
            using var api = new TimeWebApi();

            bool update = false;

            if (config.AccessToken == null || config.AccessTokenExpirationDate == null)
            {
                update = true;
            }
            else
            {
                api.SetAccessToken(config.AccessToken);

                try
                {
                    await api.S3Bucket.ListBucketsAsync();
                }
                catch (TimewebNet.Exceptions.BadCodeException badCodeE) when (badCodeE.Code == System.Net.HttpStatusCode.Forbidden)
                {
                    update = true;

                    _logger.LogWarning("Таймвеб форбиден.");
                }

                if ((config.AccessTokenExpirationDate - DateTimeOffset.UtcNow) < TimeSpan.FromDays(14))
                {
                    update = true;
                }
            }

            if (update)
            {
                await UpdateTokenAsync();
            }

            _logger.LogInformation("Проверка успешно завершена.");
        }

        public override async Task InitAsync()
        {
            if (config.AccessToken == null || config.AccessTokenExpirationDate == null ||
                config.AccessTokenExpirationDate - DateTimeOffset.UtcNow < TimeSpan.FromDays(14))
            {
                await UpdateTokenAsync();
            }
            else
            {
                api.SetAccessToken(config.AccessToken);
            }

            {
                _logger.LogInformation("Создаём ведро...");

                var createResponse = await api.S3Bucket.CreateBucketAsync(guid.ToString("N"), true, s3Type);

                _logger.LogInformation("Ищем ведро...");
                while (true)
                {
                    await Task.Delay(5000);

                    var listResponse = await api.S3Bucket.ListBucketsAsync();

                    bucket = listResponse.Storages.FirstOrDefault(i => i.Id == createResponse.Storage.Id);

                    if (bucket != null)
                        break;
                }

                pricer = new TimeBasedPricer(DateTimeOffset.UtcNow, new Bill(Currency.RUB, perHourCost));
            }

            string username = bucket.Name.Split('-')[0];
            string secret = bucket.Password;

            s3Client = new MinioClient().WithCredentials(username, secret)
                                        .WithEndpoint(endpoint)
                                        .WithRegion(bucket.Region)
                                        .WithSSL()
                                        .WithTimeout((int)config.RequestsTimeout.TotalMilliseconds)
                                        .Build();

            _logger.LogInformation("Дело сделано.");

            Ready = true;
        }

        public override async Task PutDataAsync(int id, Stream contentStream, long length)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (bucket == null)
                throw new NullReferenceException($"{nameof(bucket)} is null");

            await s3Client.PutObjectAsync(new PutObjectArgs().WithBucket(bucket.Name)
                                                             .WithStreamData(contentStream)
                                                             .WithObjectSize(length)
                                                             .WithObject($"{id}.ts"));
        }

        public override async Task ReadDataAsync(int id, long offset, long length, Stream inputStream)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (bucket == null)
                throw new NullReferenceException($"{nameof(bucket)} is null");

            await s3Client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket.Name)
                                                             .WithObject($"{id}.ts")
                                                             .WithCallbackStream(stream => stream.CopyTo(inputStream)));

        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public override async Task DestroyAsync()
        {
            if (bucket != null)
            {
                _logger.LogInformation("Удаляем ведро...");

                await api.S3Bucket.DeleteBucketAsync(bucket.Id);

                _logger.LogInformation("Ведро удалили.");
            }

            api.Dispose();
            s3Client?.Dispose();
        }
    }
}
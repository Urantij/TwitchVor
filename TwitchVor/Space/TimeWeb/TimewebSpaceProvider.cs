using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Minio;
using TimewebNet.Models;
using TimeWebNet;

namespace TwitchVor.Space.TimeWeb
{
    public class TimewebSpaceProvider : BaseSpaceProvider
    {
        private const string endpoint = "s3.timeweb.com";

        readonly TimewebConfig config;

        readonly TimeWebApi api;

        ListBucketsResponseModel.StorageModel? bucket;
        MinioClient? s3Client;

        public override bool AsyncUpload => true;

        public TimewebSpaceProvider(Guid guid, ILoggerFactory loggerFactory, TimewebConfig config)
            : base(guid, loggerFactory)
        {
            this.config = config;

            this.api = new TimeWebApi();
        }

        public override async Task InitAsync()
        {
            if ((config.AccessTokenExpirationDate - DateTimeOffset.UtcNow) < TimeSpan.FromDays(14))
            {
                _logger.LogInformation("Обновляем токен...");

                var auth = await api.GetTokenAsync(config.RefreshToken);

                config.RefreshToken = auth.Refresh_token;
                config.AccessToken = auth.Access_token;
                config.AccessTokenExpirationDate = DateTimeOffset.UtcNow.AddSeconds(auth.Expires_in);

                await Program.config.SaveAsync();

                _logger.LogInformation("Обновили");
            }

            {
                _logger.LogInformation("Создаём ведро...");

                long bucketId = await api.CreateBucketAsync(guid.ToString("N"), S3ServiceType.Start);

                _logger.LogInformation("Ищем ведро...");
                while (true)
                {
                    await Task.Delay(5000);

                    var list = await api.ListBucketsAsync();

                    bucket = list.FirstOrDefault(i => i.Id == bucketId);

                    if (bucket != null)
                        break;
                }
            }

            string username = bucket.Name.Split('-')[0];
            string secret = bucket.Password;

            s3Client = new MinioClient().WithCredentials(username, secret)
                                        .WithEndpoint(endpoint)
                                        .WithRegion(bucket.Region)
                                        .WithSSL()
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

                await api.DeleteBucketAsync(bucket.Id);

                _logger.LogInformation("Ведро удалили.");
            }

            api.Dispose();
            s3Client?.Dispose();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using TimewebNet.Models;
using TimeWebNet;
using TwitchVor.Utility;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Space.TimeWeb
{
    public class TimewebSpaceProvider : BaseSpaceProvider
    {
        const decimal perHourCost = 349M / 30M / 24M;
        const S3ServiceType s3Type = S3ServiceType.Lite;

        private const string serviceUrl = "https://s3.timeweb.com";

        const long tempFileSizeLimit = 100 * 1024 * 1024;

        readonly TimewebConfig config;

        readonly TimeWebApi api;

        ListBucketsResponseModel.BucketModel? bucket;
        AmazonS3Client? s3Client;
        S3RelatedInfo? s3Info;

        int tempNum = 0;
        FileStream? currentTempFs;

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

                    bucket = listResponse.Buckets.FirstOrDefault(i => i.Id == createResponse.Bucket.Id);

                    if (bucket != null)
                        break;
                }

                pricer = new TimeBasedPricer(DateTimeOffset.UtcNow, new Bill(Currency.RUB, perHourCost));
            }

            AmazonS3Config configsS3 = new()
            {
                ServiceURL = serviceUrl,
            };
            s3Client = new(bucket.Access_key, bucket.Secret_key, configsS3);

            string objectName = guid.ToString("N");
            var response = await s3Client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
            {
                BucketName = bucket.Name,
                Key = objectName
            });

            s3Info = new S3RelatedInfo(bucket.Name, objectName, response.UploadId);

            _logger.LogInformation("Дело сделано.");

            Ready = true;
        }

        public override Task PutDataAsync(int id, Stream contentStream, long length, CancellationToken cancellationToken = default)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (s3Info == null)
                throw new NullReferenceException($"{nameof(s3Info)} is null");

            if (currentTempFs == null || currentTempFs.Length >= tempFileSizeLimit)
            {
                var preSwapFs = currentTempFs;

                string name = $"{guid:N}_{tempNum++}";
                currentTempFs = new FileStream(DependencyProvider.MakePath(name), FileMode.Create);

                if (preSwapFs != null)
                {
                    _ = Task.Run(() => FinishTempFileAsync(s3Client, s3Info, preSwapFs), cancellationToken);
                }
            }

            return contentStream.CopyStreamAsync(currentTempFs, (int)length, cancellationToken);
        }

        public override async Task ReadAllDataAsync(Stream inputStream, long length, long offset, CancellationToken cancellationToken = default)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (s3Info == null)
                throw new NullReferenceException($"{nameof(s3Info)} is null");

            using var response = await s3Client.GetObjectAsync(new GetObjectRequest()
            {
                BucketName = s3Info.bucketName,
                Key = s3Info.objectName,
                ByteRange = new ByteRange(offset, offset + length),
            }, cancellationToken);

            await response.ResponseStream.CopyToAsync(inputStream, cancellationToken);
        }

        // public override async Task ReadPartDataAsync(int id, long offset, long length, Stream inputStream, CancellationToken cancellationToken = default)
        // {
        //     if (s3Client == null)
        //         throw new NullReferenceException($"{nameof(s3Client)} is null");
        //     if (bucket == null)
        //         throw new NullReferenceException($"{nameof(bucket)} is null");

        //     // Непонятно, можно ли тут использовать асинхронным метод в колбеке.
        //     // Код разбросан так, что пыпец.

        //     await s3Client.GetObjectAsync(new GetObjectArgs().WithBucket(bucket.Name)
        //                                                      .WithObject($"{id}.ts")
        //                                                      .WithCallbackStream(stream => stream.CopyToAsync(inputStream, cancellationToken).GetAwaiter().GetResult()), cancellationToken);
        // }

        public override async Task CloseAsync()
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (s3Info == null)
                throw new NullReferenceException($"{nameof(s3Info)} is null");

            if (currentTempFs != null)
            {
                await FinishTempFileAsync(s3Client, s3Info, currentTempFs);
                await Task.Delay(TimeSpan.FromSeconds(10));

                await s3Client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest()
                {
                    BucketName = s3Info.bucketName,
                    Key = s3Info.objectName,
                    UploadId = s3Info.uploadId,
                    PartETags = s3Info.etags
                });

                currentTempFs = null;
            }
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

        async Task FinishTempFileAsync(AmazonS3Client s3Client, S3RelatedInfo s3Info, FileStream fs)
        {
            try
            {
                const int attemptsLimit = 5;
                int attempt = 1;
                int num = s3Info.nextPartNumber++;

                Exception lastE = new();
                while (attempt <= attemptsLimit)
                {
                    fs.Position = 0;

                    try
                    {
                        var response = await s3Client.UploadPartAsync(new UploadPartRequest()
                        {
                            BucketName = s3Info.bucketName,
                            Key = s3Info.objectName,
                            PartNumber = num,
                            UploadId = s3Info.uploadId,

                            InputStream = fs,
                        });

                        // if (response.HttpStatusCode != System.Net.HttpStatusCode.OK)
                        if (string.IsNullOrEmpty(response.ETag))
                        {
                            throw new Exception("Etag IsNullOrEmpty");
                        }

                        s3Info.SetEtag(num, response.ETag);

                        _logger.LogDebug("Успешно положили объект {num}. {attempt}", num, attempt);
                        return;
                    }
                    catch (Exception e)
                    {
                        lastE = e;
                        _logger.LogWarning("FinishTempFileAsync ({attempt}/{attemptsLimit}) {message}", attempt, attemptsLimit, e.Message);

                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }

                    attempt++;
                }

                _logger.LogCritical(lastE, "FinishTempFileAsync");

                throw lastE;
            }
            finally
            {
                await fs.DisposeAsync();
                File.Delete(fs.Name);
            }
        }
    }
}
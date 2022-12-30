using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Minio;
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

        private const string endpointUrl = "s3.timeweb.com";

        const long tempFileSizeLimit = 100 * 1024 * 1024;

        readonly TimewebConfig config;

        readonly TimeWebApi api;

        ListBucketsResponseModel.BucketModel? bucket;
        MinioClient? s3Client;
        HttpClient? s3HttpClient;
        MultipartUploadHandler? multipartUploadHandler;

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

            s3HttpClient = new HttpClient(new HttpClientHandler()
            {
                Proxy = null,
                UseProxy = false
            })
            {
                Timeout = config.DownloadRequestTimeout
            };
            s3Client = new MinioClient().WithCredentials(bucket.Access_key, bucket.Secret_key)
                            .WithEndpoint(endpointUrl)
                            .WithRegion(bucket.Location)
                            .WithSSL()
                            .WithTimeout((int)config.DownloadRequestTimeout.TotalMilliseconds)
                            .WithHttpClient(s3HttpClient)
                            .Build();

            {
                string objectName = "video" + guid.ToString("N");

                var args = new NewMultipartUploadPutArgs().WithBucket(bucket.Name).WithObject(objectName);

                multipartUploadHandler = await s3Client.CreateMultipartUploadAsync(args, CancellationToken.None);
            }

            _logger.LogInformation("Дело сделано.");

            Ready = true;
        }

        public override async Task PutDataAsync(int id, Stream contentStream, long length, CancellationToken cancellationToken = default)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (multipartUploadHandler == null)
                throw new NullReferenceException($"{nameof(multipartUploadHandler)} is null");

            if (currentTempFs == null || currentTempFs.Length >= tempFileSizeLimit)
            {
                var preSwapFs = currentTempFs;

                string name = $"{guid:N}_{tempNum++}";
                currentTempFs = new FileStream(DependencyProvider.MakePath(name), FileMode.Create);

                if (preSwapFs != null)
                {
                    _ = Task.Run(() => FinishTempFileAsync(s3Client, multipartUploadHandler, preSwapFs), cancellationToken);
                }
            }

            using var timeoutCts = new CancellationTokenSource(config.DownloadRequestTimeout);
            using var resultCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            await contentStream.CopyStreamAsync(currentTempFs, (int)length, resultCts.Token);
        }

        public override async Task ReadAllDataAsync(Stream inputStream, long length, long offset, CancellationToken cancellationToken = default)
        {
            if (s3Client == null)
                throw new NullReferenceException($"{nameof(s3Client)} is null");
            if (multipartUploadHandler == null)
                throw new NullReferenceException($"{nameof(multipartUploadHandler)} is null");

            using var timeoutCts = new CancellationTokenSource(config.DownloadRequestTimeout);
            using var resultCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

            await s3Client.GetObjectAsync(new GetObjectArgs().WithBucket(multipartUploadHandler.bucketName)
                                                              .WithObject(multipartUploadHandler.objectName)
                                                              .WithOffsetAndLength(offset, length)
                                                              .WithCallbackStream(stream => stream.CopyToAsync(inputStream, resultCts.Token).GetAwaiter().GetResult()), resultCts.Token);
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
            if (multipartUploadHandler == null)
                throw new NullReferenceException($"{nameof(multipartUploadHandler)} is null");

            if (currentTempFs != null)
            {
                await FinishTempFileAsync(s3Client, multipartUploadHandler, currentTempFs);
                await Task.Delay(TimeSpan.FromSeconds(10));

                await multipartUploadHandler.CompleteAsync();

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
            s3HttpClient?.Dispose();
        }

        async Task FinishTempFileAsync(MinioClient s3Client, MultipartUploadHandler multipartUploadHandler, FileStream fs)
        {
            try
            {
                const int attemptsLimit = 5;
                int attempt = 1;
                int num = multipartUploadHandler.Reserver();

                Exception lastE = new();
                while (attempt <= attemptsLimit)
                {
                    DateTimeOffset startDate = DateTimeOffset.UtcNow;
                    fs.Position = 0;

                    try
                    {
                        using var cts = new CancellationTokenSource(config.UploadRequestTimeout);
                        var cancellationToken = cts.Token;

                        await multipartUploadHandler.PutObjectAsync(fs, partNumber: num, cancellationToken: cancellationToken);
                        DateTimeOffset endDate = DateTimeOffset.UtcNow;
                        var passed = endDate - startDate;

                        _logger.LogDebug("Успешно положили объект {num} ({seconds:F0} секунд). {attempt}", num, passed.TotalSeconds, attempt);
                        return;
                    }
                    catch (Exception e)
                    {
                        DateTimeOffset endDate = DateTimeOffset.UtcNow;
                        var passed = endDate - startDate;

                        lastE = e;
                        _logger.LogWarning("FinishTempFileAsync {num} ({seconds:F0} секунд) ({attempt}/{attemptsLimit}) {message}", num, passed.TotalSeconds, attempt, attemptsLimit, e.Message);

                        await Task.Delay(TimeSpan.FromSeconds(30));
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
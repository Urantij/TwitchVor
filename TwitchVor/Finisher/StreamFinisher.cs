using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Conversion;
using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.Space;
using TwitchVor.Twitch.Downloader;
using TwitchVor.Upload;
using TwitchVor.Utility;

namespace TwitchVor.Finisher
{
    class StreamFinisher
    {
        const int takeCount = 200;

        readonly Ffmpeg? ffmpeg;

        readonly StreamHandler streamHandler;
        readonly StreamDatabase db;
        readonly BaseSpaceProvider space;

        public StreamFinisher(StreamHandler streamHandler)
        {
            ffmpeg = Program.ffmpeg;

            this.streamHandler = streamHandler;
            db = streamHandler.db;
            space = streamHandler.space;
        }

        void Log(string message)
        {
            //TODO айди
            ColorLog.Log(message, "Finisher");
        }

        public async Task DoAsync()
        {
            long sizeLimit;
            TimeSpan durationLimit;
            {
                var _uploader = DependencyProvider.GetUploader(Guid.Empty);

                sizeLimit = _uploader.SizeLimit;
                durationLimit = _uploader.DurationLimit;
            }

            Queue<VideoFormatDb> formats = new(await db.LoadAllVideoFormatsAsync());

            VideoFormatDb currentFormat = formats.Dequeue();

            formats.TryDequeue(out VideoFormatDb? nextFormat);

            int totalSegmentsCount = await db.CountSegmentsAsync();

            bool allSuccess = true;

            int tookCount = 0;
            int videoNumber = 0;
            while (true)
            {
                SegmentDb? startSegment = null;
                SegmentDb? endSegment = null;

                int startTookIndex = tookCount;
                int videoTook = 0;

                long currentSize = 0;
                TimeSpan currentDuration = TimeSpan.Zero;
                while (true)
                {
                    var segments = await db.LoadSegmentsAsync(takeCount, tookCount);

                    if (segments.Length == 0)
                    {
                        break;
                    }
                    else
                    {
                        foreach (var segment in segments)
                        {
                            TimeSpan duration = TimeSpan.FromSeconds(segment.Duration);

                            if (currentSize + segment.Size > sizeLimit ||
                                currentDuration + duration > durationLimit)
                            {
                                // пора резать
                                break;
                            }

                            if (nextFormat != null && segment.ProgramDate >= nextFormat.Date)
                            {
                                // Он не должен записывать формат, если он тот же, но я мб передумаю в будущем.
                                bool changed = nextFormat.Format != currentFormat.Format;

                                currentFormat = nextFormat;
                                formats.TryDequeue(out nextFormat);

                                if (changed)
                                {
                                    // Сменился формат - нужно новое видео, туда же писать уже нельзя.

                                    break;
                                }
                            }

                            if (startSegment == null)
                            {
                                startSegment = segment;
                            }

                            endSegment = segment;

                            tookCount++;
                            videoTook++;

                            currentSize += segment.Size;
                            currentDuration += duration;
                        }
                    }
                }

                if (videoTook != 0)
                {
                    DateTimeOffset startDate = startSegment!.ProgramDate;
                    DateTimeOffset endDate = endSegment!.ProgramDate.AddSeconds(endSegment.Duration);

                    bool singleVideo = videoNumber == 0 && totalSegmentsCount == videoTook;

                    var uploader = DependencyProvider.GetUploader(streamHandler.guid);

                    bool success;
                    try
                    {
                        await DoVideo(videoNumber, startTookIndex, videoTook, currentSize, uploader, singleVideo, startDate, endDate);

                        success = true;
                    }
                    catch (Exception e)
                    {
                        success = false;
                    }

                    if (!success)
                        allSuccess = false;

                    videoNumber++;
                }
                else
                {
                    break;
                }
            }

            await streamHandler.DestroyAsync(destroySegments: allSuccess);

            if (Program.emailer != null)
            {
                if (allSuccess)
                {
                    await Program.emailer.SendFinishSuccessAsync();
                }
                else
                {
                    await Program.emailer.SendCriticalErrorAsync("Не получилось нормально закончить стрим...");
                }
            }

            if (Program.shutdown)
            {
                Log("Shutdown...");
                using Process pr = new();

                pr.StartInfo.FileName = "shutdown";

                pr.StartInfo.UseShellExecute = false;
                pr.StartInfo.RedirectStandardOutput = true;
                pr.StartInfo.RedirectStandardError = true;
                pr.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                pr.StartInfo.CreateNoWindow = true;
                pr.Start();

                pr.OutputDataReceived += (s, e) => { Log(e.Data); };
                pr.ErrorDataReceived += (s, e) => { Log(e.Data); };

                pr.BeginOutputReadLine();
                pr.BeginErrorReadLine();

                pr.WaitForExit();
            }
        }

        async Task<bool> DoVideo(int videoNumber, int startTookIndex, int videoTook, long size, BaseUploader uploader, bool singleVideo, DateTimeOffset startDate, DateTimeOffset endDate)
        {
            int limitIndex = startTookIndex + videoTook;

            string filename = $"result{videoNumber}." + (ffmpeg != null ? "mp4" : "ts");

            // Сюда пишем то, что будет читать аплоадер.
            // Когда закончим писать, его нужно будет закрыть.
            using var serverPipe = new AnonymousPipeServerStream(PipeDirection.Out);
            // Это читает аплоадер.
            using var clientPipe = new AnonymousPipeClientStream(PipeDirection.In, serverPipe.ClientSafePipeHandle);

            // Сюда пишем то, что вычитали из сегментов.
            Stream inputPipe;

            // Если конверсии нет, пишем сегменты в инпут (сервер пайп)
            // Если есть, пишем сегменты в инпут (инпут ффмпега), и пишем аутпут ффмпега в сервер пайп
            ConversionHandler? conversionHandler = null;
            if (ffmpeg != null)
            {
                conversionHandler = ffmpeg.CreateConversion();

                inputPipe = conversionHandler.InputStream;

                // // Читать ффмпег
                // _ = Task.Run(async () =>
                // {
                //     while (true)
                //     {
                //         var line = await conversionHandler.TextStream.ReadLineAsync();

                //         if (line == null)
                //         {
                //             System.Console.WriteLine("ффмпег закончил говорить.");
                //             return;
                //         }

                //         System.Console.WriteLine(line);
                //     }
                // });

                // Перенаправление выхода ффмпега в сервер.
                _ = Task.Run(async () =>
                {
                    await conversionHandler.OutputStream.CopyToAsync(serverPipe);
                    await conversionHandler.OutputStream.FlushAsync();

                    await serverPipe.DisposeAsync();

                    System.Console.WriteLine("Закончился выход у ффмпега.");
                });
            }
            else
            {
                inputPipe = serverPipe;
            }

            // Чтение сегментов, перенаправление в инпут.
            _ = Task.Run(async () =>
            {
                long offset = await db.CalculateOffsetAsync(startTookIndex);

                for (int index = startTookIndex; index < limitIndex; index += takeCount)
                {
                    int take = Math.Min(takeCount, limitIndex - index);

                    SegmentDb[] segments = await db.LoadSegmentsAsync(take, index);

                    foreach (var segment in segments)
                    {
                        await space.ReadDataAsync(segment.Id, offset, segment.Size, inputPipe);

                        offset += segment.Size;
                    }
                }

                await inputPipe.FlushAsync();
                await inputPipe.DisposeAsync();
            });

            var skips = await db.LoadSkipsAsync();

            string[] subgifters = await DescriptionMaker.GetDisplaySubgiftersAsync(streamHandler.subCheck);

            string videoName = DescriptionMaker.FormVideoName(streamHandler.handlerCreationDate, singleVideo ? null : videoNumber, 100, streamHandler.timestamper.timestamps);

            TimeSpan totalLostTime = TimeSpan.FromTicks(skips.Sum(s => (s.EndDate - s.StartDate).Ticks));

            string description = DescriptionMaker.FormDescription(startDate, streamHandler.timestamper.timestamps, skips, subgifters, streamHandler.streamDownloader.AdvertismentTime, totalLostTime);

            bool success = await uploader.UploadAsync(videoName, description, filename, size, clientPipe);

            if (conversionHandler != null)
            {
                await conversionHandler.WaitAsync();

                conversionHandler.Dispose();
            }

            return success;
        }
    }
}
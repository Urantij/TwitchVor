using TwitchStreamDownloader.Queues;
using TwitchVor.Utility;

namespace TwitchVor.Vvideo
{
    /// <summary>
    /// Этот класс пишет сегменты в указанный файл, а также следит, какие сегменты были первыми и последними
    /// </summary>
    class VideoWriter
    {
        public readonly FileThing linkedThing;

        private FileStream fileStream;

        private readonly Queue<QueueItem> writeQueue = new();
        public readonly List<SkipInfo> skipInfos = new();

        public bool temp;

        private bool writing = false;
        private TaskCompletionSource? writingOperation = null;

        private bool closed = false;

        public VideoWriter(FileThing linkedThing, bool temp)
        {
            this.linkedThing = linkedThing;
            this.temp = temp;

            fileStream = new FileStream(linkedThing.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        void Log(string message)
        {
            ColorLog.Log(message, $"VideoWriter {linkedThing.FileName}", ConsoleColor.DarkGreen);
        }

        void LogWarning(string message)
        {
            ColorLog.LogWarning(message, $"VideoWriter {linkedThing.FileName}");
        }

        void LogError(string message)
        {
            ColorLog.LogError(message, $"VideoWriter {linkedThing.FileName}");
        }

        void LogDebug(string message)
        {
            if (Program.debug)
                Log(message);
        }

        public void Write(QueueItem queueItem)
        {
            lock (this)
            {
                if (closed)
                {
                    LogWarning($"Attempt to queue item in closed video writer.");
                    return;
                }

                writeQueue.Enqueue(queueItem);

                linkedThing.estimatedDuration += queueItem.segment.duration;
                linkedThing.estimatedSize += queueItem.bufferWriteStream.Length;

                if (writing)
                    return;

                writing = true;
                writingOperation = new();
                Task.Run(Worker);
            }
        }

        /// <summary>
        /// Ждёт, когда закончит свою работу работник
        /// </summary>
        /// <returns></returns>
        public async Task Wait()
        {
            Task workTask;
            lock (this)
            {
                if (writingOperation == null)
                {
                    //я посмотрел, шансов, что тут нулл, а ещё есть хуйня, нет.
                    return;
                }

                workTask = writingOperation.Task;
            }

            await workTask;
        }

        public void OpenFileStream()
        {
            fileStream = new FileStream(linkedThing.FilePath, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public void CloseFileStream()
        {
            fileStream.Dispose();
        }

        /// <summary>
        /// Закрывает видеопроигрыватель и ждёт если нужно, когда дозапишется всё.
        /// </summary>
        public async Task CloseAsync()
        {
            if (closed) 
            {
                LogWarning($"Attempt to close closed video writer.");
                return;
            }
            closed = true;

            Log($"Closing video writer...");

            await Wait();

            fileStream.Dispose();

            Log($"Closed video writer.");
        }

        private async void Worker()
        {
            while (!closed)
            {
                QueueItem? item;
                lock (this)
                {
                    if (!writeQueue.TryDequeue(out item))
                    {
                        writing = false;

                        //похуй ведь, что оно в локе это делает?
                        //точно не нулл.
                        writingOperation!.SetResult();
                        writingOperation = null;
                        return;
                    }
                }

                try
                {
                    LogDebug($"Saving segment {item.segment.mediaSequenceNumber} to file");

                    await SaveFileAsync(item.bufferWriteStream);

                    if (linkedThing.firstSegmentDate == null)
                    {
                        linkedThing.firstSegmentDate = item.segment.programDate.UtcDateTime;
                    }

                    if (linkedThing.lastSegmentEndDate != null)
                    {
                        var passed = item.segment.programDate - linkedThing.lastSegmentEndDate.Value;

                        if (passed >= Program.config.MinimumSegmentSkipDelay)
                        {
                            lock (skipInfos)
                            {
                                skipInfos.Add(new SkipInfo(linkedThing.lastSegmentEndDate.Value, item.segment.programDate.UtcDateTime));
                            }

                            Log($"Skip Detected! Skipped {passed.TotalSeconds} seconds :(");
                        }
                    }

                    linkedThing.lastSegmentEndDate = item.segment.programDate.AddSeconds(item.segment.duration).UtcDateTime;
                }
                catch (Exception e)
                {
                    LogError($"Could not write segment to file\n{e}");
                    linkedThing.estimatedDuration -= item.segment.duration;
                    linkedThing.estimatedSize -= item.bufferWriteStream.Length;
                }

                try
                {
                    item.bufferWriteStream.Dispose();
                }
                catch (Exception e)
                {
                    LogError($"Could not dispose content stream\n{e}");
                }
            }
        }

        private async Task SaveFileAsync(Stream contentStream)
        {
            using var timeoutCancellationSource = new CancellationTokenSource(Program.config.FileWriteTimeout);

            await contentStream.CopyToAsync(fileStream, timeoutCancellationSource.Token);
        }

        public static string GenerateFileName(DateTime date)
        {
            return date.ToString("yyyy_MM_dd-HH_mm_ss") + ".ts";
        }
    }
}
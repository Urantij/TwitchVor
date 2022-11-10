using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Data.Models;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Finisher;

/// <summary>
/// Хранит необходимую для обработки стрима информацию.
/// </summary>
class ProcessingHandler
{
    readonly TaskCompletionSource processTCS = new();

    public readonly TimeSpan advertismentLoss;
    public readonly TimeSpan totalLoss;

    public readonly Bill[] bills;

    public readonly IEnumerable<BaseTimestamp> timestamps;
    public readonly IEnumerable<SkipDb> skips;
    public readonly ProcessingVideo[] videos;

    public readonly string[] subgifters;

    /// <summary>
    /// Сюда можно класть вещи, а потом копаться в них и брать нужное.
    /// Я подумал, и не придумал ничего лучше.
    /// </summary>
    public readonly List<object> trashcan = new();

    public Task ProcessTask => processTCS.Task;

    public ProcessingHandler(TimeSpan advertismentLoss, TimeSpan totalLoss, Bill[] bills, IEnumerable<BaseTimestamp> timestamps, IEnumerable<SkipDb> skips, ProcessingVideo[] videos, string[] subgifters)
    {
        this.advertismentLoss = advertismentLoss;
        this.totalLoss = totalLoss;
        this.bills = bills;
        this.timestamps = timestamps;
        this.skips = skips;
        this.videos = videos;
        this.subgifters = subgifters;
    }

    public void SetResult()
    {
        processTCS.SetResult();
    }

    public string MakeVideoDescription(ProcessingVideo video)
    {
        TimeSpan? videoUploadTime = video.uploadEnd - video.uploadStart;
        TimeSpan? totalUploadTime = SumTotalUploadTime();

        return DescriptionMaker.FormDescription(video.startDate, timestamps, skips, subgifters, advertismentLoss, totalLoss, bills, videoUploadTime, totalUploadTime);
    }

    TimeSpan? SumTotalUploadTime()
    {
        var uploads = videos.Where(v => v.uploadStart != null && v.uploadEnd != null)
                            .Select(v => (v.uploadEnd!.Value - v.uploadStart!.Value).Ticks)
                            .ToArray();

        if (uploads.Length == 0)
            return null;

        return TimeSpan.FromTicks(uploads.Sum());
    }
}

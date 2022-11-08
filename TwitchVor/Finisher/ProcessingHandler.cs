using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Data.Models;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Finisher;

public class ProcessingHandler
{
    readonly TaskCompletionSource processTCS = new();

    public readonly TimeSpan advertismentLoss;
    public readonly TimeSpan totalLoss;

    public readonly Bill[] bills;

    public readonly IEnumerable<SkipDb> skips;
    public readonly ProcessingVideo[] videos;

    /// <summary>
    /// Сюда можно класть вещи, а потом копаться в них и брать нужное.
    /// Я подумал, и не придумал ничего лучше.
    /// </summary>
    public readonly List<object> trashcan = new();

    public Task ProcessTask => processTCS.Task;

    public ProcessingHandler(TimeSpan advertismentLoss, TimeSpan totalLoss, Bill[] bills, IEnumerable<SkipDb> skips, ProcessingVideo[] videos)
    {
        this.advertismentLoss = advertismentLoss;
        this.totalLoss = totalLoss;
        this.bills = bills;
        this.skips = skips;
        this.videos = videos;
    }

    public void SetResult()
    {
        processTCS.SetResult();
    }
}

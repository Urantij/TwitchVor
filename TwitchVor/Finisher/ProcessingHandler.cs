using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Data;
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

    /// <summary>
    /// UTC
    /// </summary>
    public readonly DateTime handlerCreationDate;

    public readonly StreamDatabase db;

    public readonly TimeSpan advertismentLoss;
    public readonly TimeSpan totalLoss;

    public readonly Bill[] bills;

    public readonly IEnumerable<BaseTimestamp> timestamps;
    public readonly IEnumerable<SkipDb> skips;

    public readonly List<ResultVideoSizeCache> videoSizeCaches = new();

    public readonly Dota2Dispenser.Shared.Models.MatchModel[]? dotaMatches;

    public readonly string[] subgifters;

    public Task ProcessTask => processTCS.Task;

    public ProcessingHandler(DateTime handlerCreationDate, StreamDatabase db, TimeSpan advertismentLoss,
        TimeSpan totalLoss, Bill[] bills, IEnumerable<BaseTimestamp> timestamps, IEnumerable<SkipDb> skips,
        string[] subgifters, Dota2Dispenser.Shared.Models.MatchModel[]? dotaMatches)
    {
        this.handlerCreationDate = handlerCreationDate;
        this.db = db;
        this.advertismentLoss = advertismentLoss;
        this.totalLoss = totalLoss;
        this.bills = bills;
        this.timestamps = timestamps;
        this.skips = skips;
        this.subgifters = subgifters;
        this.dotaMatches = dotaMatches;
    }

    public void SetResult()
    {
        processTCS.SetResult();
    }
}
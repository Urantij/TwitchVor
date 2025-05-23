using TwitchVor.Data;
using TwitchVor.Data.Models;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Money;

namespace TwitchVor.Finisher;

/// <summary>
/// Хранит необходимую для обработки стрима информацию.
/// </summary>
internal class ProcessingHandler
{
    private readonly TaskCompletionSource processTCS = new();

    /// <summary>
    /// UTC
    /// </summary>
    public readonly DateTime handlerCreationDate;

    public readonly StreamDatabase db;

    public readonly TimeSpan advertismentLoss;
    public readonly TimeSpan totalLoss;

    public readonly Bill[] bills;

    public readonly IReadOnlyList<BaseTimestamp> timestamps;
    public readonly IReadOnlyList<SkipDb> skips;

    public readonly List<ResultVideoSizeCache> videoSizeCaches = new();

    public readonly Dota2Dispenser.Shared.Models.MatchModel[]? dotaMatches;

    public readonly string[] subgifters;

    /// <summary>
    /// Завершается, когда закончили работать все загрузчики.
    /// </summary>
    public Task ProcessTask => processTCS.Task;

    public ProcessingHandler(DateTime handlerCreationDate, StreamDatabase db, TimeSpan advertismentLoss,
        TimeSpan totalLoss, Bill[] bills, IReadOnlyList<BaseTimestamp> timestamps, IReadOnlyList<SkipDb> skips,
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
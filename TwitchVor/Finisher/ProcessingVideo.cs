using TwitchVor.Data.Models;

namespace TwitchVor.Finisher;

public class ProcessingVideo
{
    public readonly int number;

    public readonly int segmentStart;

    // TODO Подумать, насколько это хорошая идея, не хранить последний индекс сегмента, а надеяться, что ни одного айди пропущено не будет.
    public readonly int segmentsCount;

    public readonly long size;

    public readonly DateTimeOffset startDate;
    public readonly DateTimeOffset endDate;

    public readonly TimeSpan loss;

    /// <summary>
    /// true успешно загрузилось
    /// false успешно не загрузилось
    /// null ещё не закончилась загрузка
    /// </summary>
    public bool? success;

    public DateTimeOffset? processingStart;
    public DateTimeOffset? processingEnd;

    public DateTimeOffset? uploadStart;
    public DateTimeOffset? uploadEnd;

    public ProcessingVideo(int number, int segmentStart, int segmentsLength, long size, DateTimeOffset startDate,
        DateTimeOffset endDate, TimeSpan loss)
    {
        this.number = number;
        this.segmentStart = segmentStart;
        this.segmentsCount = segmentsLength;
        this.size = size;
        this.startDate = startDate;
        this.endDate = endDate;
        this.loss = loss;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="absoluteDate"></param>
    /// <param name="skips">Скипы, чьё начало было ДО <paramref name="absoluteDate"/></param>
    /// <returns></returns>
    public TimeSpan GetOnVideoTime(DateTimeOffset absoluteDate, IEnumerable<SkipDb> skips)
    {
        return GetOnVideoTime(startDate, absoluteDate, skips);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="videoStartDate"></param>
    /// <param name="absoluteDate"></param>
    /// <param name="skips">Скипы, чьё начало было ДО <paramref name="absoluteDate"/></param>
    /// <returns></returns>
    public static TimeSpan GetOnVideoTime(DateTimeOffset videoStartDate, DateTimeOffset absoluteDate,
        IEnumerable<SkipDb> skips)
    {
        //Время на видео, это абсолютное время (date) минус все скипы, которые произошли до этого момента минус время начала видео

        DateTimeOffset result = absoluteDate;

        var ourSkips = skips.Where(skip => skip.StartDate < absoluteDate).ToArray();

        foreach (SkipDb skip in ourSkips)
        {
            // Если скип целиком входит в видео, берём скип целиком.
            // Если скип входит лишь частично, берём его часть.

            if (skip.StartDate >= absoluteDate)
                break;

            DateTimeOffset endDate = skip.EndDate <= absoluteDate ? skip.EndDate : absoluteDate;

            result -= (endDate - skip.StartDate);
        }

        return result - videoStartDate;
    }
}
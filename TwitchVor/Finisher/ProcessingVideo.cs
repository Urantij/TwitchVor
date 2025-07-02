using TwitchVor.Data.Models;

namespace TwitchVor.Finisher;

public class ProcessingVideo
{
    public readonly int number;

    public readonly int startingSegmentId;
    public readonly int endingSegmentId;

    // В теории их может быть не енд минус старт, если мы часть сегментов не берём (если сохраняю рекламные, но не записываю)
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

    public ProcessingVideo(int number, int startingSegmentId, int endingSegmentId, int segmentsCount, long size, DateTimeOffset startDate,
        DateTimeOffset endDate, TimeSpan loss)
    {
        this.number = number;
        this.startingSegmentId = startingSegmentId;
        this.endingSegmentId = endingSegmentId;
        this.segmentsCount = segmentsCount;
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
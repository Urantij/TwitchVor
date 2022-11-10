using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchVor.Data.Models;

namespace TwitchVor.Finisher;

public class ProcessingVideo
{
    public readonly int number;

    public readonly int segmentStart;
    public readonly int segmentsLength;

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

    public DateTimeOffset? uploadStart;
    public DateTimeOffset? uploadEnd;

    public ProcessingVideo(int number, int segmentStart, int segmentsLength, long size, DateTimeOffset startDate, DateTimeOffset endDate, TimeSpan loss)
    {
        this.number = number;
        this.segmentStart = segmentStart;
        this.segmentsLength = segmentsLength;
        this.size = size;
        this.startDate = startDate;
        this.endDate = endDate;
        this.loss = loss;
    }
}

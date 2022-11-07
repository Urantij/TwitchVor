using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Finisher;

public class ProcessingVideo
{
    public readonly int number;

    public readonly int segmentStart;
    public readonly int segmentsLength;

    public readonly long size;

    public readonly DateTimeOffset startDate;
    public readonly DateTimeOffset endDate;

    public DateTimeOffset? uploadStart;
    public DateTimeOffset? uploadEnd;

    public ProcessingVideo(int number, int segmentStart, int segmentsLength, long size, DateTimeOffset startDate, DateTimeOffset endDate)
    {
        this.number = number;
        this.segmentStart = segmentStart;
        this.segmentsLength = segmentsLength;
        this.size = size;
        this.startDate = startDate;
        this.endDate = endDate;
    }
}

using Microsoft.Extensions.Logging;

namespace TwitchVor.EventIds;

public static class StreamFinisherEvents
{
    public static readonly EventId UploadingEventId = new(1);
}
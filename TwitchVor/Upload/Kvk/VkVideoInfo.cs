using TwitchVor.Finisher;

namespace TwitchVor.Upload.Kvk;

public class VkVideoInfo
{
    public readonly ProcessingVideo video;
    public readonly long id;

    public VkVideoInfo(ProcessingVideo video, long id)
    {
        this.video = video;
        this.id = id;
    }

    // https://vk.com/video-216880923_456239411
    public string ToLink(long groupId)
    {
        return $"https://vk.com/video-{groupId}_{id}";
    }
}
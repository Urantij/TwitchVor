namespace TwitchVor.Vvideo.Pubg;

public class PubgMatchTimestamp : BaseTimestamp
{
    public PubgMatchTimestamp(DateTime timestamp) : base(timestamp)
    {
    }

    public override string MakeString()
    {
        return "Новый раунд";
    }
}
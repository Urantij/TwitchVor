namespace TwitchVor.Vvideo.Pubg;

public class PubgMatchTimestamp : BaseTimestamp
{
    private readonly PubgMatch _match;

    public PubgMatchTimestamp(PubgMatch match) : base(match.StartDate)
    {
        _match = match;
    }

    public override string MakeString()
    {
        return $"Новый раунд ({_match.MapName})";
    }
}
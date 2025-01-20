namespace TwitchVor.Vvideo.Pubg;

public class PubgInVideoCache(string lastKnownMatchId)
{
    public string LastKnownMatchId { get; set; } = lastKnownMatchId;
}
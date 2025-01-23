namespace TwitchVor.Vvideo.Dota;

public class DotaInVideoConfig
{
    public required ulong TargetSteamId { get; set; }
    public required Uri DispenserUrl { get; set; }

    public string HeroesPath { get; set; } = "./DotaHeroes.json";

    /// <summary>
    /// Писать ли в описании видео, чем закончилась игра.
    /// </summary>
    public bool SpoilResults { get; set; } = false;

    public DotaInVideoConfig(ulong targetSteamId, Uri dispenserUrl)
    {
        TargetSteamId = targetSteamId;
        DispenserUrl = dispenserUrl;
    }
}
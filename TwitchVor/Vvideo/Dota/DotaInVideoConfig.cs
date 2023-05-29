using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TwitchVor.Vvideo.Dota;

public class DotaInVideoConfig
{
    public required ulong TargetSteamId { get; set; }
    public required Uri DispenserUrl { get; set; }

    public string HeroesPath { get; set; } = "./DotaHeroes.json";

    public DotaInVideoConfig(ulong targetSteamId, Uri dispenserUrl)
    {
        TargetSteamId = targetSteamId;
        DispenserUrl = dispenserUrl;
    }
}

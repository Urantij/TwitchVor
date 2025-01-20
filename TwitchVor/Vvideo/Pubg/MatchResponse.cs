using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TwitchVor.Vvideo.Pubg;

public class MatchResponse
{
    [JsonConstructor]
    public MatchResponse(
        [JsonProperty("data")] MatchData matchData,
        [JsonProperty("included")] JArray included
    )
    {
        this.MatchData = matchData;
        this.Included = included;
    }

    [JsonProperty("data")] public MatchData MatchData { get; }

    [JsonProperty("included")] public JArray Included { get; }
}

public class MatchAttributes
{
    [JsonConstructor]
    public MatchAttributes(
        [JsonProperty("gameMode")] string gameMode,
        [JsonProperty("titleId")] string titleId,
        [JsonProperty("matchType")] string matchType,
        [JsonProperty("mapName")] string mapName,
        [JsonProperty("isCustomMatch")] bool? isCustomMatch,
        [JsonProperty("seasonState")] string seasonState,
        [JsonProperty("createdAt")] DateTime? createdAt,
        [JsonProperty("duration")] int? duration,
        [JsonProperty("stats")] object stats,
        [JsonProperty("shardId")] string shardId
    )
    {
        this.GameMode = gameMode;
        this.TitleId = titleId;
        this.MatchType = matchType;
        this.MapName = mapName;
        this.IsCustomMatch = isCustomMatch;
        this.SeasonState = seasonState;
        this.CreatedAt = createdAt;
        this.Duration = duration;
        this.Stats = stats;
        this.ShardId = shardId;
    }

    [JsonProperty("gameMode")] public string GameMode { get; }

    [JsonProperty("titleId")] public string TitleId { get; }

    [JsonProperty("matchType")] public string MatchType { get; }

    [JsonProperty("mapName")] public string MapName { get; }

    [JsonProperty("isCustomMatch")] public bool? IsCustomMatch { get; }

    [JsonProperty("seasonState")] public string SeasonState { get; }

    [JsonProperty("createdAt")] public DateTime? CreatedAt { get; }

    [JsonProperty("duration")] public int? Duration { get; }

    [JsonProperty("stats")] public object Stats { get; }

    [JsonProperty("shardId")] public string ShardId { get; }
}

public class MatchData
{
    [JsonConstructor]
    public MatchData(
        [JsonProperty("type")] string type,
        [JsonProperty("id")] string id,
        [JsonProperty("attributes")] MatchAttributes attributes
    )
    {
        this.Type = type;
        this.Id = id;
        this.Attributes = attributes;
    }

    [JsonProperty("type")] public string Type { get; }

    [JsonProperty("id")] public string Id { get; }

    [JsonProperty("attributes")] public MatchAttributes Attributes { get; }
}

//

public class IncludedPlayer
{
    [JsonConstructor]
    public IncludedPlayer(
        [JsonProperty("type")] string type,
        [JsonProperty("id")] string id,
        [JsonProperty("attributes")] PlayerAttributes attributes
    )
    {
        this.Type = type;
        this.Id = id;
        this.Attributes = attributes;
    }

    [JsonProperty("type")] public string Type { get; }

    [JsonProperty("id")] public string Id { get; }

    [JsonProperty("attributes")] public PlayerAttributes Attributes { get; }
}

public class PlayerAttributes
{
    [JsonConstructor]
    public PlayerAttributes(
        [JsonProperty("stats")] Stats stats,
        [JsonProperty("actor")] string actor,
        [JsonProperty("shardId")] string shardId
    )
    {
        this.Stats = stats;
        this.Actor = actor;
        this.ShardId = shardId;
    }

    [JsonProperty("stats")] public Stats Stats { get; }

    [JsonProperty("actor")] public string Actor { get; }

    [JsonProperty("shardId")] public string ShardId { get; }
}

public class Stats
{
    [JsonConstructor]
    public Stats(
        [JsonProperty("DBNOs")] int? dBNOs,
        [JsonProperty("assists")] int? assists,
        [JsonProperty("boosts")] int? boosts,
        [JsonProperty("damageDealt")] int? damageDealt,
        [JsonProperty("deathType")] string deathType,
        [JsonProperty("headshotKills")] int? headshotKills,
        [JsonProperty("heals")] int? heals,
        [JsonProperty("killPlace")] int? killPlace,
        [JsonProperty("killStreaks")] int? killStreaks,
        [JsonProperty("kills")] int? kills,
        [JsonProperty("longestKill")] int? longestKill,
        [JsonProperty("name")] string name,
        [JsonProperty("playerId")] string playerId,
        [JsonProperty("revives")] int? revives,
        [JsonProperty("rideDistance")] int? rideDistance,
        [JsonProperty("roadKills")] int? roadKills,
        [JsonProperty("swimDistance")] int? swimDistance,
        [JsonProperty("teamKills")] int? teamKills,
        [JsonProperty("timeSurvived")] int? timeSurvived,
        [JsonProperty("vehicleDestroys")] int? vehicleDestroys,
        [JsonProperty("walkDistance")] double? walkDistance,
        [JsonProperty("weaponsAcquired")] int? weaponsAcquired,
        [JsonProperty("winPlace")] int? winPlace
    )
    {
        this.DBNOs = dBNOs;
        this.Assists = assists;
        this.Boosts = boosts;
        this.DamageDealt = damageDealt;
        this.DeathType = deathType;
        this.HeadshotKills = headshotKills;
        this.Heals = heals;
        this.KillPlace = killPlace;
        this.KillStreaks = killStreaks;
        this.Kills = kills;
        this.LongestKill = longestKill;
        this.Name = name;
        this.PlayerId = playerId;
        this.Revives = revives;
        this.RideDistance = rideDistance;
        this.RoadKills = roadKills;
        this.SwimDistance = swimDistance;
        this.TeamKills = teamKills;
        this.TimeSurvived = timeSurvived;
        this.VehicleDestroys = vehicleDestroys;
        this.WalkDistance = walkDistance;
        this.WeaponsAcquired = weaponsAcquired;
        this.WinPlace = winPlace;
    }

    [JsonProperty("DBNOs")] public int? DBNOs { get; }

    [JsonProperty("assists")] public int? Assists { get; }

    [JsonProperty("boosts")] public int? Boosts { get; }

    [JsonProperty("damageDealt")] public int? DamageDealt { get; }

    [JsonProperty("deathType")] public string DeathType { get; }

    [JsonProperty("headshotKills")] public int? HeadshotKills { get; }

    [JsonProperty("heals")] public int? Heals { get; }

    [JsonProperty("killPlace")] public int? KillPlace { get; }

    [JsonProperty("killStreaks")] public int? KillStreaks { get; }

    [JsonProperty("kills")] public int? Kills { get; }

    [JsonProperty("longestKill")] public int? LongestKill { get; }

    [JsonProperty("name")] public string Name { get; }

    [JsonProperty("playerId")] public string PlayerId { get; }

    [JsonProperty("revives")] public int? Revives { get; }

    [JsonProperty("rideDistance")] public int? RideDistance { get; }

    [JsonProperty("roadKills")] public int? RoadKills { get; }

    [JsonProperty("swimDistance")] public int? SwimDistance { get; }

    [JsonProperty("teamKills")] public int? TeamKills { get; }

    [JsonProperty("timeSurvived")] public int? TimeSurvived { get; }

    [JsonProperty("vehicleDestroys")] public int? VehicleDestroys { get; }

    [JsonProperty("walkDistance")] public double? WalkDistance { get; }

    [JsonProperty("weaponsAcquired")] public int? WeaponsAcquired { get; }

    [JsonProperty("winPlace")] public int? WinPlace { get; }
}
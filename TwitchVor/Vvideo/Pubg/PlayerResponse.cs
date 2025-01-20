using Newtonsoft.Json;

namespace TwitchVor.Vvideo.Pubg;

public class AccountAttributes
{
    [JsonConstructor]
    public AccountAttributes(
        [JsonProperty("name")] string name,
        [JsonProperty("stats")] object stats,
        [JsonProperty("titleId")] string titleId,
        [JsonProperty("shardId")] string shardId,
        [JsonProperty("patchVersion")] string patchVersion,
        [JsonProperty("banType")] string banType,
        [JsonProperty("clanId")] string clanId
    )
    {
        this.Name = name;
        this.Stats = stats;
        this.TitleId = titleId;
        this.ShardId = shardId;
        this.PatchVersion = patchVersion;
        this.BanType = banType;
        this.ClanId = clanId;
    }

    [JsonProperty("name")] public string Name { get; }

    [JsonProperty("stats")] public object Stats { get; }

    [JsonProperty("titleId")] public string TitleId { get; }

    [JsonProperty("shardId")] public string ShardId { get; }

    [JsonProperty("patchVersion")] public string PatchVersion { get; }

    [JsonProperty("banType")] public string BanType { get; }

    [JsonProperty("clanId")] public string ClanId { get; }
}

public class AccountData
{
    [JsonConstructor]
    public AccountData(
        [JsonProperty("type")] string type,
        [JsonProperty("id")] string id,
        [JsonProperty("attributes")] AccountAttributes accountAttributes,
        [JsonProperty("relationships")] Relationships relationships
    )
    {
        this.Type = type;
        this.Id = id;
        this.AccountAttributes = accountAttributes;
        this.Relationships = relationships;
    }

    /// <summary>
    /// player
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; }

    [JsonProperty("id")] public string Id { get; }

    [JsonProperty("attributes")] public AccountAttributes AccountAttributes { get; }

    [JsonProperty("relationships")] public Relationships Relationships { get; }
}

public class Matches
{
    [JsonConstructor]
    public Matches(
        [JsonProperty("data")] List<Match> data
    )
    {
        this.Data = data;
    }

    [JsonProperty("data")] public IReadOnlyList<Match> Data { get; }
}

public class Match
{
    [JsonConstructor]
    public Match([JsonProperty("type")] string type,
        [JsonProperty("id")] string id)
    {
        Type = type;
        Id = id;
    }

    /// <summary>
    /// match
    /// </summary>
    [JsonProperty("type")]
    public string Type { get; }

    [JsonProperty("id")] public string Id { get; }
}

public class Relationships
{
    [JsonConstructor]
    public Relationships(
        [JsonProperty("matches")] Matches matches
    )
    {
        this.Matches = matches;
    }

    [JsonProperty("matches")] public Matches Matches { get; }
}

public class PlayerResponse
{
    [JsonConstructor]
    public PlayerResponse(
        [JsonProperty("data")] AccountData accountData
    )
    {
        this.AccountData = accountData;
    }

    [JsonProperty("data")] public AccountData AccountData { get; }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TwitchVor.Vvideo.Dota;

public class HeroModel
{
    [JsonPropertyName("id")] public required int Id { get; set; }
    [JsonPropertyName("name")] public required string Name { get; set; }
    [JsonPropertyName("localized_name")] public string? LocalizedName { get; set; }

    public HeroModel(int id, string name, string? localizedName)
    {
        Id = id;
        Name = name;
        LocalizedName = localizedName;
    }
}
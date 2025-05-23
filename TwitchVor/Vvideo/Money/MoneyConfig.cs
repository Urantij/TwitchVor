using Newtonsoft.Json;

namespace TwitchVor.Vvideo.Money;

public class MoneyConfig
{
    [JsonProperty(Required = Required.Always)]
    public Currency Currency { get; set; }

    [JsonProperty(Required = Required.Always)]
    public decimal PerHourCost { get; set; }
}
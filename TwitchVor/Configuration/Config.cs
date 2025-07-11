using Newtonsoft.Json;
using TwitchVor.Communication.Email;
using TwitchVor.Conversion;
using TwitchVor.Twitch;
using TwitchVor.Twitch.Chat;
using TwitchVor.Upload.Kvk;
using TwitchVor.Upload.TubeYou;
using TwitchVor.Vvideo.Dota;
using TwitchVor.Vvideo.Money;
using TwitchVor.Vvideo.Pubg;

namespace TwitchVor.Configuration;

internal class Config
{
    [JsonIgnore] private string path;

    [Obsolete("Юзается для десериализации, не трогай.")]
    public Config()
    {
    }

    public Config(string path)
    {
        this.path = path;
    }

    public async Task SaveAsync()
    {
        string content = JsonConvert.SerializeObject(this, Formatting.Indented);

        await File.WriteAllTextAsync(path, content);
    }

    public static async Task<Config> LoadAsync(string path)
    {
        string content = await File.ReadAllTextAsync(path);

        var config = JsonConvert.DeserializeObject<Config>(content)!;
        config.path = path;

        return config;
    }

    [JsonProperty(Required = Required.Default)]
    public MoneyConfig Money { get; set; }

    [JsonProperty(Required = Required.Always)]
    public string? Channel { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public string? ChannelId { get; set; } = null;

    [JsonProperty(Required = Required.AllowNull)]
    public string? TwitchAPIClientId { get; set; } = null;

    [JsonProperty(Required = Required.AllowNull)]
    public string? TwitchAPISecret { get; set; } = null;

    /// <summary>
    /// 1920x1080 Source
    /// Если сурс, фпс игнорируется
    /// </summary>
    public string PreferedVideoResolution { get; set; } = "Source";
    public float PreferedVideoFps { get; set; } = 60;
    public bool TakeOnlyPrefered { get; set; } = false;

    /// <summary>
    /// Почему-то вк не хочет нормально обрабатывать видео. Я хочу сохранить видео.
    /// </summary>
    public bool SaveTheVideo { get; set; } = false;

    [JsonProperty(Required = Required.Default)]
    public ChatConfig? Chat { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public DotaInVideoConfig? Dota { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public PubgInVideoConfig? Pubg { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public VkCreds? Vk { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public YoutubeCreds? Youtube { get; set; } = null;

    [JsonProperty(Required = Required.Default)]
    public ConversionConfig? Conversion { get; set; } = null;

    //huh

    [JsonProperty(Required = Required.Default)]
    public EmailConfig? Email { get; set; }

    //Checker

    /// <summary>
    /// Как часто хеликс проверяет стрим
    /// </summary>
    [JsonProperty(Required = Required.Default)]
    public TimeSpan HelixCheckDelay { get; set; } = TimeSpan.FromSeconds(22);

    [JsonProperty(Required = Required.Default)]
    public TimeSpan PubsubReconnectDelay { get; set; } = TimeSpan.FromSeconds(10);

    //Stream

    [JsonProperty(Required = Required.Always)]
    public DownloaderConfig Downloader { get; set; }

    /// <summary>
    /// У токена срока жизни 20 минут, но пользоваться им можно час, почему то.
    /// Тру - форсить смену токена через 20 минут (ну или когда он истечёт(?))
    /// Юзлес, если юзается токен без oauth, так как такой токен хоть тыщу лет юзать можно (наверное)
    /// </summary>
    public bool DownloaderForceTokenChange { get; set; } = false;

    /// <summary>
    /// Как долго считать офнутый стрим не офнутым
    /// </summary>
    public TimeSpan StreamContinuationCheckTime { get; set; } = TimeSpan.FromSeconds(22); //секунд 22

    /// <summary>
    /// Как долго ждать переподруба
    /// </summary>
    public TimeSpan StreamRestartCheckTime { get; set; } = TimeSpan.FromHours(1); //часик

    public TimeSpan SegmentDownloaderTimeout { get; set; } = TimeSpan.FromSeconds(5);

    //File

    public string CacheDirectoryName { get; set; } = "CachedData";

    /// <summary>
    /// Информация о длительности сегмента не всегда правдива.
    /// Поэтому какую то погрешность заложим.
    /// Но если больше, то мы потеряли контент
    /// </summary>
    public TimeSpan MinimumSegmentSkipDelay { get; set; } = TimeSpan.FromSeconds(0.2);

    public int UnstableSpaceAttempsLimit { get; set; } = 3;

    public bool Manual { get; set; } = false;

    public bool MapOnTheFly { get; set; } = true;
}
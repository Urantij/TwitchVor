using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TwitchVor.Data;

namespace TwitchVor.Twitch.Downloader;

public class MapInfo(string url, int dbId, byte[] bytes)
{
    public string Url { get; } = url;
    public int DbId { get; } = dbId;
    public byte[] Bytes { get; } = bytes;
}

/// <summary>
/// Хранит начальные сегменты из map тегов. Качает их, если нужно, но всё хранит в памяти.
/// И пишет их в бд кстати.
/// Ну они маленькие, несколько килобайт всего.
/// </summary>
public class MapContainer
{
    // URI="https://c513a515af41.j.cloudfront.hls.ttvnw.net/v1/segment/CtQCr-fbhmD925_INBtMddsm3cL7Q7iWFLxIAcMsUgwd6KloQmeimoczDjyvfuHfeskmaIlvQqmjrxvcCjAjR7-PW5xm-fKyPrL9bq3ETpIElmYpmdjftkuEbXfrnE4o68xsB5Bw0_b_o4RI57zX6StQULNgtfstOgEhXXQDqbut98BFGe_v2Nb3pjxJ_5f1tWL9RnMsSwfomgqixSDIMBkEFWS5NoZ_m_mVd0zgWAYHRsKGYC1TZ3zh1-D5lz1WE8UQNawNHAlmHNDbHOvFYl3T52n0VyOaqREMxgc1IV1lOFqKxf5iezkCpNiXLwAclooUV50Bn1Fo9NKJajjXiQeqg7vfxmaY5BhY2_E9FIdL5B4tKjKjoVFf7Z0ZabQQ1dxWZN0po1TEzfH1ZVmvzidCFyX5tRMhfbvsZjfik4S_pJmyk2DKCJuU04bHnCwT-gXL4EBiXRoMTPojpbeZr7-0EemaIAEqCWV1LXdlc3QtMjDODA.mp4?dna=CmmxpHN7Z82FYvtUvmIv69P131JG5Mtu4NPKnUIyptQnpvWCHpCHmoCYGAnLQLFK9OioFzPt-ai70gS5mNEasHXYF_j5G40Ngwk2c192L58qYyxWpRzRFDO86pFpF7VqMSGsPQixlhpADk4aDGhh-OiYrfc0twVu2CABKglldS13ZXN0LTIwzgw"
    private static readonly Regex Regex = new("URI=\"(?<url>.+)\"", RegexOptions.Compiled);

    private readonly HttpClient _client;
    private readonly StreamDatabase _database;
    private readonly ILogger _logger;

    private readonly List<MapInfo> _list = new();

    public MapContainer(HttpClient client, StreamDatabase database, ILogger logger)
    {
        _client = client;
        _database = database;
        _logger = logger;
    }

    public MapInfo FirstMapByDbId(int dbId)
    {
        return _list.First(m => m.DbId == dbId);
    }

    public async Task<MapInfo> GetMappedAsync(string tagValue, CancellationToken cancellationToken = default)
    {
        Match match = Regex.Match(tagValue);

        if (!match.Success)
        {
            throw new Exception($"Неизвестное значение мап тега. {tagValue}");
        }

        string url = match.Groups["url"].Value;

        MapInfo? value = _list.FirstOrDefault(m => m.Url == url);

        if (value != null)
        {
            return value;
        }

        _logger.LogInformation("Грузим мапу... {url}", url);

        byte[] mapContent = await _client.GetByteArrayAsync(url, cancellationToken);

        int dbId = await _database.AddMapAsync(mapContent.Length);

        value = new MapInfo(url, dbId, mapContent);

        _list.Add(value);

        return value;
    }
}
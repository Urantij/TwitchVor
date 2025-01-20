using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TwitchVor.Vvideo.Pubg;

public class PubgMatch
{
    public PubgMatch(DateTime startDate, string mapName)
    {
        StartDate = startDate;
        MapName = mapName;
    }

    public DateTime StartDate { get; set; }
    public string MapName { get; set; }
}

public class PubgInVideo
{
    private readonly ILogger _logger;
    private readonly PubgInVideoConfig _config;
    private readonly HttpClient _httpClient;

    private readonly string _cachePath = "./pubg.cache.json";

    public PubgInVideo(PubgInVideoConfig config, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PubgInVideo>();
        _config = config;
        _httpClient = new HttpClient();
    }

    public async Task<List<PubgMatch>> GetMatchesAsync(DateTime startFrom)
    {
        PlayerResponse player = await GetPlayerAsync();

        string? lastKnownMatchId = null;
        if (File.Exists(_cachePath))
        {
            string cacheContent = await File.ReadAllTextAsync(_cachePath);
            var cache = JsonConvert.DeserializeObject<PubgInVideoCache>(cacheContent);
            lastKnownMatchId = cache.LastKnownMatchId;
        }

        // В теории, если чето сломается, я не хочу, чтобы он листал матчи до бесконечности. Там он 400 штук возвращает
        const int limit = 100;
        int madeRequests = 0;
        List<PubgMatch> result = new();
        foreach (Match match in player.AccountData.Relationships.Matches.Data)
        {
            if (match.Id == lastKnownMatchId)
                break;

            if (madeRequests > limit)
                break;

            MatchResponse response;
            try
            {
                madeRequests++;
                response = await GetMatchAsync(match.Id);
            }
            catch (Exception e)
            {
                _logger.LogError("Не удалось скачать матч: {err}", e.Message);
                continue;
            }

            if (response.MatchData.Attributes.CreatedAt == null)
            {
                _logger.LogWarning("Матч {id} без времени ({type})", match.Id, response.MatchData.Attributes.MatchType);
                continue;
            }

            if (response.MatchData.Attributes.CreatedAt < startFrom)
                break;

            result.Add(new PubgMatch(response.MatchData.Attributes.CreatedAt.Value, response.MatchData.Attributes.MapName));
        }

        if (player.AccountData.Relationships.Matches.Data.Count > 0)
        {
            lastKnownMatchId = player.AccountData.Relationships.Matches.Data[0].Id;

            string content = JsonConvert.SerializeObject(new PubgInVideoCache(lastKnownMatchId));
            await File.WriteAllTextAsync(_cachePath, content);
        }

        _logger.LogInformation("Загрузили {count} матчей, мы взяли {}",
            player.AccountData.Relationships.Matches.Data.Count, result.Count);

        return result;
    }

    private async Task<PlayerResponse> GetPlayerAsync()
    {
        using HttpRequestMessage requestMessage =
            new(HttpMethod.Get, $"https://api.pubg.com/shards/steam/players/{_config.AccountId}");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        using HttpResponseMessage response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();

        var result = JsonConvert.DeserializeObject<PlayerResponse>(content);

        if (result == null)
        {
            throw new Exception("result is null");
        }

        return result;
    }

    private async Task<MatchResponse> GetMatchAsync(string matchId)
    {
        using HttpRequestMessage requestMessage =
            new(HttpMethod.Get, $"https://api.pubg.com/shards/steam/matches/{matchId}");
        // requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
        requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));

        using HttpResponseMessage response = await _httpClient.SendAsync(requestMessage);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync();

        var result = JsonConvert.DeserializeObject<MatchResponse>(content);

        if (result == null)
        {
            throw new Exception("result is null");
        }

        return result;
    }
}
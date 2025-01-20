using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace TwitchVor.Vvideo.Pubg;

public class PubgMatch
{
    public PubgMatch(DateTime startDate)
    {
        StartDate = startDate;
    }

    public DateTime StartDate { get; set; }
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

        Match[] matches = player.AccountData.Relationships.Matches.Data.Take(20)
            .TakeWhile(match => match.Id != lastKnownMatchId).ToArray();

        List<PubgMatch> result = new(matches.Length);

        foreach (Match match in matches)
        {
            MatchResponse response;
            try
            {
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
                continue;

            result.Add(new PubgMatch(response.MatchData.Attributes.CreatedAt.Value));
        }

        if (matches.Length > 0)
        {
            lastKnownMatchId = matches[0].Id;

            string content = JsonConvert.SerializeObject(new PubgInVideoCache(lastKnownMatchId));
            await File.WriteAllTextAsync(_cachePath, content);
        }

        _logger.LogInformation("Загрузили {count} матчей, нормальных {okay}", matches.Length, result.Count);

        return result;
    }

    async Task<PlayerResponse> GetPlayerAsync()
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

    async Task<MatchResponse> GetMatchAsync(string matchId)
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
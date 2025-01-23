using System.Collections.Specialized;
using System.Text.Json;
using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Vvideo.Dota;

public class DotaInVideo
{
    private readonly ILogger logger;

    readonly HttpClient httpClient;
    public readonly DotaInVideoConfig config;

    public DotaInVideo(DotaInVideoConfig config, ILoggerFactory loggerFactory)
    {
        this.logger = loggerFactory.CreateLogger(this.GetType());

        httpClient = new();
        this.config = config;
    }

    public async ValueTask TestAsync()
    {
        HeroModel[] heroes;
        try
        {
            heroes = await LoadHeroesAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось загрузить героев при проверке.");
            return;
        }

        logger.LogInformation("Загружено {count} героев.", heroes.Length);

        try
        {
            await LoadMatchesAsync(limit: 1, useTarget: false);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Не удалось загрузить матчи при проверке.");
            return;
        }

        logger.LogInformation("Загрузили матч.");
    }

    public async Task<HeroModel[]> LoadHeroesAsync()
    {
        var content = await File.ReadAllTextAsync(config.HeroesPath);

        return JsonSerializer.Deserialize<HeroModel[]>(content)!;
    }

    public HeroModel[] LoadHeroes()
    {
        var content = File.ReadAllText(config.HeroesPath);

        return JsonSerializer.Deserialize<HeroModel[]>(content)!;
    }

    public async Task<MatchModel[]> LoadMatchesAsync(DateTime? afterTime = null, int? limit = null,
        bool useTarget = true)
    {
        NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        if (useTarget)
            queryString[Dota2DispenserParams.steamIdFilter] = config.TargetSteamId.ToString();
        if (afterTime != null)
            queryString[Dota2DispenserParams.afterDateTimeFilter] =
                new DateTimeOffset(afterTime.Value).ToUnixTimeSeconds().ToString();
        if (limit != null)
            queryString[Dota2DispenserParams.limitFilter] = limit.ToString();

        Uri uri = new(config.DispenserUrl, $"match?{queryString}");

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead);

        response.EnsureSuccessStatusCode();

        string responseContent = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<MatchModel[]>(responseContent)!;
    }
}
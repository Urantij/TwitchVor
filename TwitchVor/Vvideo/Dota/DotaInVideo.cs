using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
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

    public async Task<MatchModel[]> LoadMatchesAsync(DateTime afterTime)
    {
        NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
        queryString[Dota2DispenserParams.steamIdFilter] = config.TargetSteamId.ToString();
        queryString[Dota2DispenserParams.afterDateTimeFilter] = new DateTimeOffset(afterTime).ToUnixTimeSeconds().ToString();

        Uri uri = new(config.DispenserUrl, $"{Dota2DispenserPoints.matches}?{queryString}");

        using var response = await httpClient.GetAsync(uri, HttpCompletionOption.ResponseContentRead);

        response.EnsureSuccessStatusCode();

        string responseContent = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<MatchModel[]>(responseContent)!;
    }
}

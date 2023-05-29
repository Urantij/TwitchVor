using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Data.Models;
using TwitchVor.Twitch;
using TwitchVor.Utility;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Money;
using TwitchVor.Vvideo.Timestamps;

namespace TwitchVor.Finisher
{
    static class DescriptionMaker
    {
        static ILogger? _logger;

        public static void SetLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(DescriptionMaker));
        }

        public static async Task<string[]> GetDisplaySubgiftersAsync(SubCheck? streamSubcheck)
        {
            List<SubCheck> tempList = new();

            if (streamSubcheck != null)
                tempList.Add(streamSubcheck);

            if (Program.subChecker != null)
            {
                SubCheck? postStreamSubCheck = await Program.subChecker.GetSubAsync();

                if (postStreamSubCheck != null)
                    tempList.Add(postStreamSubCheck);
            }

            return tempList.Where(s => s.sub)
                           .Reverse() //ник мог поменяться, тогда нужно юзать самый новый
                           .DistinctBy(sc => sc.subInfo?.GiftertId)
                           .Select(sc =>
                           {
                               if (sc.subInfo == null)
                                   return "???";

                               if (sc.subInfo.GifterName.Equals(sc.subInfo.GifterLogin, StringComparison.OrdinalIgnoreCase))
                               {
                                   return sc.subInfo.GifterName;
                               }

                               return $"{sc.subInfo.GifterName} ({sc.subInfo.GifterLogin})";

                           }).ToArray();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="date">Дата стрима</param>
        /// <param name="videoNumber">Номер видоса (нулл, если видос один)</param>
        /// <param name="limit">Лимит буковок. У ютуба это 100, бтв</param>
        /// <returns></returns>
        public static string FormVideoName(DateTimeOffset date, int? videoNumber, int limit, IEnumerable<BaseTimestamp> timestamps)
        {
            const string gamesSeparator = ", ";
            const string gamesMany = "...";

            StringBuilder builder = new();

            builder.Append(date.ToString("dd.MM.yyyy"));

            if (videoNumber != null)
            {
                builder.Append(" // ");
                builder.Append(videoNumber.Value);
            }

            string[] games = timestamps.Where(timestamp => timestamp is GameTimestamp)
                                       .Select(timestamp => ((GameTimestamp)timestamp).gameName ?? "???")
                                       .Distinct()
                                       .ToArray();

            if (games.Length > 0)
            {
                builder.Append(" // ");

                for (int i = 0; i < games.Length; i++)
                {
                    string game = games[i];
                    string? nextGame = (i + 1) < games.Length ? games[i + 1] : null;

                    int length = game.Length;
                    if (nextGame != null)
                    {
                        length += gamesSeparator.Length + nextGame.Length;
                    }

                    if (builder.Length + length <= limit)
                    {
                        builder.Append(game);

                        if (nextGame != null)
                            builder.Append(gamesSeparator);
                    }
                    else if (builder.Length + gamesMany.Length <= limit)
                    {
                        builder.Append(gamesMany);
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return builder.ToString();
        }

        public static string FormDescription(DateTimeOffset videoStartDate, IEnumerable<BaseTimestamp> timestamps, IEnumerable<SkipDb> skips, string[] subgifters, TimeSpan advertismentTime, TimeSpan totalLostTime, Bill[] bills, TimeSpan? videoUploadTime, TimeSpan? totalUploadTime, Dota2Dispenser.Shared.Models.MatchModel[]? dotaMatches)
        {
            StringBuilder builder = new();
            builder.AppendLine("Здесь ничего нет, в будущем я стану человеком");

            if (subgifters.Length > 0)
            {
                builder.AppendLine();
                foreach (var subgifter in subgifters)
                {
                    builder.AppendLine($"Спасибо за подписку: {subgifter}");
                }
            }

            if (!timestamps.Any())
            {
                builder.AppendLine();
                builder.AppendLine("Инфы нет, потому что я клоун");
            }
            else
            {
                builder.AppendLine();

                bool first = true;
                foreach (var timestamp in timestamps)
                {
                    if (timestamp is OfflineTimestamp)
                        continue;

                    string status;
                    if (first)
                    {
                        first = false;

                        status = MakeTimestampStr(TimeSpan.FromSeconds(0), timestamp.ToString());
                    }
                    else
                    {
                        status = GetCheckStatusString(timestamp, videoStartDate, skips);
                    }

                    builder.AppendLine(status);
                }
            }

            if (dotaMatches?.Length > 0)
            {
                // В теории всё может сломаться, если пропадёт файл с героями.
                // TODO Вообще плохо, что идёт работа в классе, который создаёт описание. Как-то предзагружать надо.
                // Или сделать ещё одну обёртку под матчи + героев и класть в процесс хендлер.
                try
                {
                    var heroes = Program.dota!.LoadHeroes();
                    var matchesLines = dotaMatches.Select(match =>
                    {
                        var streamer = match.Players?.FirstOrDefault(p => p.SteamId == Program.dota.config.TargetSteamId);

                        if (streamer == null)
                            return "???";

                        var onVideoTime = ProcessingVideo.GetOnVideoTime(videoStartDate, match.GameDate, skips);
                        
                        StringBuilder sb = new();

                        string heroName = heroes.FirstOrDefault(hero => hero.Id == streamer.HeroId)?.LocalizedName ?? "Непонятно";

                        sb.Append(heroName);

                        if (match.DetailsInfo?.RadiantWin != null)
                        {
                            // Я выбрал не тот тип для коллекции, хдд
                            // Я надеюсь, первые пять это редиант.
                            int index = match.Players!.ToList().IndexOf(streamer);

                            bool win = index <= 4 ? match.DetailsInfo.RadiantWin == true : match.DetailsInfo.RadiantWin == false;

                            sb.Append(win ? " Вин" : " Луз");
                        }

                        int partyCount;
                        if (streamer.PartyIndex != null)
                            partyCount = match.Players!.Count(p => p.PartyIndex == streamer.PartyIndex);
                        else
                            partyCount = 1;

                        if (partyCount > 1)
                        {
                            sb.Append($" (Пати {partyCount})");
                        }

                        return MakeTimestampStr(onVideoTime, sb.ToString());
                    }).ToArray();

                    builder.AppendLine();

                    foreach (var matchLine in matchesLines)
                        builder.AppendLine(matchLine);
                }
                catch (Exception e)
                {
                    _logger?.LogError(e, "Не удалось сформировать описание матчей доты.");
                }
            }

            string? uploadTimeSpentString = MakeTimeSpentString("Загрузка заняла:", videoUploadTime, totalUploadTime);
            if (uploadTimeSpentString != null)
            {
                builder.AppendLine();

                builder.AppendLine(uploadTimeSpentString);
            }

            builder.AppendLine();

            builder.AppendLine($"Пропущено секунд всего: {totalLostTime.TotalSeconds:N0}");
            builder.AppendLine($"Пропущено секунд из-за рекламы: {advertismentTime.TotalSeconds:N0}");

            if (bills.Length > 0)
            {
                var sumBills = bills.GroupBy(b => b.currency)
                                    .Select(g => new Bill(g.Key, g.Sum(b => b.count)))
                                    .Where(t => t.count > 0M)
                                    .Select(t => t.Format())
                                    .ToArray();

                if (sumBills.Length > 0)
                {
                    builder.AppendLine();

                    var result = string.Join(", ", sumBills);

                    builder.AppendLine($"Примерная стоимость создания записи стрима: {result}");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="prefix"></param>
        /// <param name="videoTime">Времени потрачено на текущий видос</param>
        /// <param name="globalTime">Времени потрачено на все видосы</param>
        /// <returns></returns>
        private static string? MakeTimeSpentString(string prefix, TimeSpan? videoTime, TimeSpan? globalTime)
        {
            if (videoTime != null && globalTime != null)
            {
                if (videoTime.Value.Ticks != globalTime.Value.Ticks)
                    return $"{prefix} {globalTime.Value.TotalMinutes:n2} ({videoTime.Value.TotalMinutes:n2}) минут.";
                else
                    return $"{prefix} {globalTime.Value.TotalMinutes:n2} минут.";
            }
            else if (videoTime != null)
            {
                return $"{prefix} ... ({videoTime.Value.TotalMinutes:n2}) минут.";
            }
            else if (globalTime != null)
            {
                return $"{prefix} {globalTime.Value.TotalMinutes:n2} минут.";
            }

            return null;
        }

        private static string GetCheckStatusString(BaseTimestamp timestamp, DateTimeOffset videoStartDate, IEnumerable<SkipDb> skips)
        {
            TimeSpan onVideoTime = ProcessingVideo.GetOnVideoTime(videoStartDate, timestamp.timestamp, skips);

            if (onVideoTime.Ticks < 0)
            {
                _logger?.LogWarning("Ticks < 0 {timestamp}", timestamp.timestamp);
                onVideoTime = TimeSpan.FromSeconds(0);
            }

            return MakeTimestampStr(onVideoTime, timestamp.ToString());
        }

        private static string MakeTimestampStr(TimeSpan onVideoTime, string content)
        {
            string timeStr = new DateTime(onVideoTime.Ticks).ToString("HH:mm:ss");

            return $"{timeStr} {content}";
        }
    }
}
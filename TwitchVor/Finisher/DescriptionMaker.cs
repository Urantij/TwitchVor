using System.Text;
using Microsoft.Extensions.Logging;
using TwitchVor.Data.Models;
using TwitchVor.Twitch;
using TwitchVor.Twitch.Chat;
using TwitchVor.Vvideo;
using TwitchVor.Vvideo.Money;
using TwitchVor.Vvideo.Timestamps;

namespace TwitchVor.Finisher;

internal static class DescriptionMaker
{
    private static ILogger? _logger;

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
    /// <param name="timestamps">Таймстампы с <see cref="GameTimestamp"/>, которые попадут в название видео</param>
    /// <returns></returns>
    public static string FormVideoName(DateTimeOffset date, int? videoNumber, int limit,
        IReadOnlyList<BaseTimestamp> timestamps)
    {
        const string gamesSeparator = ", ";
        const string gamesMany = "...";

        StringBuilder builder = new();

        builder.Append(date.ToString("dd.MM.yyyy"));

        if (videoNumber != null)
        {
            builder.Append(" // ");
            builder.Append(videoNumber.Value + 1);
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

    public static string FormDescription(DateTimeOffset videoStartDate, IReadOnlyList<BaseTimestamp> timestamps,
        IReadOnlyList<SkipDb> skips, string[] subgifters, TimeSpan advertismentTime, TimeSpan totalLostTime,
        Bill[] bills, TimeSpan? videoUploadTime, TimeSpan? totalUploadTime, string? nextVideoUrl = null,
        string? prevVideoUrl = null)
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
            foreach (BaseTimestamp timestamp in timestamps.Where(t =>
                         t is not ChatCustomTimestamp && t is not ChatClipTimestamp))
            {
                if (timestamp is OfflineTimestamp)
                    continue;

                string status;
                if (first)
                {
                    first = false;

                    status = MakeTimestampStr(TimeSpan.FromSeconds(0), timestamp.MakeString(), false);
                }
                else
                {
                    status = GetCheckStatusString(timestamp, videoStartDate, skips, false);
                }

                builder.AppendLine(status);
            }

            void DoStamps<T>() where T : BaseTimestamp
            {
                T[] theseStamps = timestamps.OfType<T>().ToArray();
                if (theseStamps.Length == 0) return;

                builder.AppendLine();

                foreach (T timestamp in theseStamps)
                {
                    string status = GetCheckStatusString(timestamp, videoStartDate, skips, true);

                    builder.AppendLine(status);
                }
            }

            DoStamps<ChatCustomTimestamp>();
            DoStamps<ChatClipTimestamp>();
        }

        if (nextVideoUrl != null || prevVideoUrl != null)
        {
            builder.AppendLine();
            if (nextVideoUrl != null)
            {
                builder.AppendLine($"Следующая часть: {nextVideoUrl}");
            }

            if (prevVideoUrl != null)
            {
                builder.AppendLine($"Предыдущая часть: {prevVideoUrl}");
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

    private static string GetCheckStatusString(BaseTimestamp timestamp, DateTimeOffset videoStartDate,
        IReadOnlyList<SkipDb> skips, bool fake)
    {
        TimeSpan onVideoTime = ProcessingVideo.GetOnVideoTime(videoStartDate, timestamp.GetTimeWithOffset(), skips);

        if (onVideoTime.Ticks < 0)
        {
            _logger?.LogWarning("Ticks < 0 {timestamp}", timestamp.GetTimeWithOffset());
            onVideoTime = TimeSpan.FromSeconds(0);
        }

        return MakeTimestampStr(onVideoTime, timestamp.MakeString(), fake);
    }

    private static string MakeTimestampStr(TimeSpan onVideoTime, string content, bool fake)
    {
        string timeStr = new DateTime(onVideoTime.Ticks).ToString("HH:mm:ss");

        if (fake)
            return $"- {timeStr} {content}";

        return $"{timeStr} {content}";
    }
}
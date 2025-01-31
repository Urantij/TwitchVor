using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using TwitchLib.Api.Helix.Models.Clips.GetClips;
using TwitchSimpleLib.Chat.Messages;
using TwitchVor.Twitch.Chat;

namespace TwitchVor.Twitch.Downloader;

internal partial class StreamChatWorker
{
    // https://www.twitch.tv/dunduk/clip/DependableLongHorseSoBayed-uWND5zAY9aef6M-q 
    private static readonly Regex ClipRegex = MyRegex();

    private readonly ILogger _logger;
    private readonly StreamHandler _handler;

    /// <summary>
    /// Просто следит за тем, чтобы не спрашивать твич о клипах больше одного раза.
    /// </summary>
    private readonly List<string> _notedClips = new();

    private readonly List<Clip> _fetchedClips = new();

    public StreamChatWorker(StreamHandler handler, ILoggerFactory loggerFactory)
    {
        _handler = handler;
        _logger = loggerFactory.CreateLogger<StreamChatWorker>();
    }

    public Clip[] CloneFetchedClips()
    {
        lock (_fetchedClips)
        {
            return _fetchedClips.ToArray();
        }
    }

    internal async void PrivateMessageReceived(object? sender, TwitchPrivateMessage priv)
    {
        try
        {
            await AddMessageToDbAsync(priv);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"PrivMsgReceived {nameof(AddMessageToDbAsync)}");
        }

        try
        {
            AddMark(priv);
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"PrivMsgReceived {nameof(AddMark)}");
        }

        try
        {
            if (Program.config.Chat?.FetchClips != null)
            {
                await ProcessClipsAsync(priv, Program.config.Chat.FetchClips);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, $"PrivMsgReceived {nameof(ProcessClipsAsync)}");
        }
    }

    private async Task AddMessageToDbAsync(TwitchPrivateMessage priv)
    {
        string? badges = priv.rawIrcMessage.tags?.GetValueOrDefault("badges");

        await _handler.db.AddChatMessageAsync(priv.userId, priv.username, priv.displayName, priv.text, priv.color,
            badges, priv.tmiSentTs);
    }

    private void AddMark(TwitchPrivateMessage priv)
    {
        // Временная мера, которая будет постоянной, потому что чатбота нормального у меня нет.
        if ((!priv.mod && !priv.vip) || (!priv.text.StartsWith("=метка ", StringComparison.OrdinalIgnoreCase) &&
                                         !priv.text.StartsWith("=м ", StringComparison.OrdinalIgnoreCase))) return;

        string text = priv.text.Split(' ', 2)[1];

        ChatCustomTimestamp stamp = new(text, priv.displayName ?? priv.username,
            DateTime.UtcNow);
        // В теории оно не может быть нулл, но чтобы иде не плакала
        stamp.SetOffset(Program.config.Chat?.TimestampOffset ?? TimeSpan.FromSeconds(-30));

        _handler.timestamper.AddTimestamp(stamp);
    }

    private async Task ProcessClipsAsync(TwitchPrivateMessage priv, ChatClipConfig config)
    {
        GetClipsResponse clips;
        {
            Match[] matches = priv.text.Split(' ')
                .Select(word => ClipRegex.Match(word))
                .Where(m => m.Success)
                .Where(m =>
                {
                    // и зачем он клип с чужого канала кинул...
                    string broadcaster = m.Groups["broadcaster"].Value;
                    if (!broadcaster.Equals(Program.config.Channel, StringComparison.OrdinalIgnoreCase))
                        return false;

                    string id = m.Groups["id"].Value;
                    return !_notedClips.Contains(id);
                })
                .ToArray();

            if (matches.Length == 0)
                return;

            List<string> clipsIds = matches.Select(m => m.Groups["id"].Value).Distinct().ToList();

            _notedClips.AddRange(clipsIds);

            clips = await Program.twitchAPI.Helix.Clips.GetClipsAsync(clipIds: clipsIds,
                startedAt: _handler.handlerCreationDate);
        }

        _logger.LogDebug("Нашли {count} клипов.", clips.Clips.Length);
        lock (_fetchedClips)
        {
            _fetchedClips.AddRange(clips.Clips);
        }
    }

    [GeneratedRegex(@"https:\/\/www\.twitch\.tv\/(?<broadcaster>[A-Za-z0-9_\-]+)\/clip\/(?<id>[A-Za-z0-9_\-]+)",
        RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}
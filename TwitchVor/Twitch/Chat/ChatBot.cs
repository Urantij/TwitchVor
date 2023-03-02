using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchSimpleLib.Chat;

namespace TwitchVor.Twitch.Chat;

public class ChatBot
{
    readonly ILogger _logger;

    public readonly TwitchChatClient client;

    public ChatBot(string channel, string? username, string? token, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        TwitchChatClientOpts opts;

        if (username != null && token != null)
        {
            opts = new(username, token);
        }
        else
        {
            opts = new();
        }

        client = new(true, opts, loggerFactory);
        client.AddAutoJoinChannel(channel);

        client.AuthFailed += AuthFailed;
        client.ChannelJoined += ChannelJoind;
        client.ConnectionClosed += ConnectionClosed;
    }

    public async Task StartAsync()
    {
        await client.ConnectAsync();
    }

    private void AuthFailed(object? sender, EventArgs e)
    {
        _logger.LogCritical("Чат бот AuthFail");
    }

    private void ChannelJoind(object? sender, string e)
    {
        _logger.LogInformation("Присоединился к каналу.");
    }

    private void ConnectionClosed(Exception? e)
    {
        _logger.LogWarning("Соединение закрыто {message}", e?.Message ?? "без сообщения");
    }

    public void Dispose()
    {
        client.Close();
    }
}

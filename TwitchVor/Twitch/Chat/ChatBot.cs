using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SimpleTwitchChatLib.Client;
using SimpleTwitchChatLib.Irc.Messages;

namespace TwitchVor.Twitch.Chat;

public class ChatBot
{
    readonly ILogger _logger;

    public readonly MyTwitchChatClient client;

    public ChatBot(string channel, string? username, string? token, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        MyTwitchOpts opts;

        if (username != null && token != null)
        {
            opts = new(username, token);
        }
        else
        {
            opts = new();
        }

        client = new(opts, channel, loggerFactory: loggerFactory);

        client.AuthFailed += AuthFailed;
        client.ChannelConnected += ChannelConnected;
        client.ConnectionClosed += ConnectionClosed;
    }

    public async Task StartAsync()
    {
        await client.StartAsync();
    }

    private void AuthFailed(object? sender, EventArgs e)
    {
        _logger.LogCritical("Чат бот AuthFail");
    }

    private void ChannelConnected(object? sender, string e)
    {
        _logger.LogInformation("Присоединился к каналу.");
    }

    private void ConnectionClosed(object? sender, Exception? e)
    {
        _logger.LogInformation("Соединение закрыто {message}", e?.Message ?? "без сообщения");
    }

    public void Dispose()
    {
        client.Close();
    }
}

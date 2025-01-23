using Microsoft.Extensions.Logging;

namespace TwitchVor.Twitch.Checker;

public abstract class BaseChecker
{
    protected readonly ILoggerFactory _loggerFactory;
    protected readonly ILogger _logger;

    public event EventHandler<TwitchCheckInfo>? ChannelChecked;

    protected BaseChecker(ILoggerFactory loggerFactory)
    {
        this._loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected void OnChannelChecked(TwitchCheckInfo twitchCheckInfo)
    {
        ChannelChecked?.Invoke(this, twitchCheckInfo);
    }
}
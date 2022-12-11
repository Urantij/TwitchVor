using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Twitch.Checker;

public abstract class BaseChecker
{
    protected ILogger _logger;

    public event EventHandler<TwitchCheckInfo>? ChannelChecked;

    protected BaseChecker(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());
    }

    protected void OnChannelChecked(TwitchCheckInfo twitchCheckInfo)
    {
        ChannelChecked?.Invoke(this, twitchCheckInfo);
    }
}

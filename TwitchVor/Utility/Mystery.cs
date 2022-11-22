using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Utility;

public static class Mystery
{
    /// <summary>
    /// Происходят непонятные вещи.
    /// </summary>
    /// <returns></returns>
    public static CancellationTokenSource MysteryCTS()
    {
        return new CancellationTokenSource(TimeSpan.FromSeconds(10));
    }
}

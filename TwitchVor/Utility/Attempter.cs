using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Utility;

public static class Attempter
{
    public static void Do(ILogger logger, Action attemptAction, Action onRetryAction)
    {
        Do(Program.config.UnstableSpaceAttempsLimit, logger, attemptAction, onRetryAction);
    }

    public static void Do(int attemptsLimit, ILogger logger, Action attemptAction, Action onRetryAction)
    {
        for (int attempt = 0; attempt < attemptsLimit; attempt++)
        {
            try
            {
                attemptAction();
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Attempt {attempt}/{limit}...", attempt, attemptsLimit);

                if (attempt + 1 == Program.config.UnstableSpaceAttempsLimit)
                {
                    throw;
                }

                onRetryAction();
            }
        }
    }

    public static async Task DoAsync(ILogger logger, Func<Task> attemptAction, Action onRetryAction)
    {
        await DoAsync(Program.config.UnstableSpaceAttempsLimit, logger, attemptAction, onRetryAction);
    }

    public static async Task DoAsync(int attemptsLimit, ILogger logger, Func<Task> attemptAction, Action onRetryAction)
    {
        for (int attempt = 0; attempt < attemptsLimit; attempt++)
        {
            try
            {
                await attemptAction();
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Attempt {attempt}/{limit}...", attempt, attemptsLimit);

                if (attempt + 1 == Program.config.UnstableSpaceAttempsLimit)
                {
                    throw;
                }

                onRetryAction();
            }
        }
    }
}

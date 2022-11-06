using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TwitchVor.Utility;

namespace TwitchVor.Utility
{
    public static class Greater
    {
        public static void Great(ILoggerFactory loggerFactory)
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(Greater));

            ConsoleColor[] colors = new ConsoleColor[]
            {
                ConsoleColor.Red,
                ConsoleColor.DarkYellow,
                ConsoleColor.Yellow,
                ConsoleColor.Green,
                ConsoleColor.Blue,
                ConsoleColor.DarkBlue,
                ConsoleColor.Magenta,
            };

            foreach (var color in colors)
            {
                string value = "пидор".ColorMe(foreground: color);

                logger.LogInformation("Я думаю, ты {value}.", value);
            }
        }
    }
}
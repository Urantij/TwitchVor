using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
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

            string[] guesses = new string[]
            {
                "Я думаю",
                "Я считаю",
                // "Я предполагаю",

                "Думаю",

                "Мне кажется",
                "Похоже",

                "Скорее всего",
                "Вероятно",
                // "Предположительно",
                "Возможно",
                "Наверное"
            };

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

            int targetLength = guesses.Max(g => g.Length);

            foreach (var color in colors)
            {
                string guess = guesses[RandomNumberGenerator.GetInt32(guesses.Length)];
                guess += ',';
                guess = guess.PadRight(targetLength + 1);

                string value = "пидор".ColorMe(foreground: color);

                logger.LogInformation("{guess} ты {value}.", guess, value);
            }
        }
    }
}
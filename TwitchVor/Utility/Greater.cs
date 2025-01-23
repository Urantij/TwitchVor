using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Utility
{
    public static class Greater
    {
        static readonly ConsoleColor[] colors = new ConsoleColor[]
        {
            ConsoleColor.Red,
            ConsoleColor.DarkYellow,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Blue,
            ConsoleColor.DarkBlue,
            ConsoleColor.Magenta,
        };

        static readonly string[] guesses = new string[]
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

        static readonly string[] values = new string[]
        {
            "чудо",
            "молодец",
            "умница"
        };

        static readonly int targetGuessLength = guesses.Max(g => g.Length);

        static ILogger? _logger;

        static int currentIndex = 0;

        public static int ColorsLength => colors.Length;

        public static void SetLogger(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(typeof(Greater));
        }

        public static void Great(string? nextString = null)
        {
            ConsoleColor color = colors[currentIndex];

            if (++currentIndex >= colors.Length)
                currentIndex = 0;

            string guess = guesses[RandomNumberGenerator.GetInt32(guesses.Length)];
            guess += ',';
            guess = guess.PadRight(targetGuessLength + 1);

            string value = values[RandomNumberGenerator.GetInt32(values.Length)].ColorMe(foreground: color);

            if (nextString != null)
            {
                _logger?.LogInformation("{guess} ты {value}.\n{nextString}", guess, value, nextString);
            }
            else
            {
                _logger?.LogInformation("{guess} ты {value}.", guess, value);
            }
        }
    }
}
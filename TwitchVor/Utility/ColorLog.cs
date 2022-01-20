using System;

namespace TwitchVor.Utility
{
    static class ColorLog
    {
        private static readonly ConsoleColor defFColor = ConsoleColor.Gray;
        private static readonly ConsoleColor defBColor = ConsoleColor.Black;

        public static void LogWarning(string message, string? tag = null)
        {
            Log(message, tag, fColor: ConsoleColor.Yellow);
        }

        public static void LogError(string message, string? tag = null)
        {
            Log(message, tag, fColor: ConsoleColor.Red);
        }

        public static void Log(string message, string? tag = null, ConsoleColor? fColor = null, ConsoleColor? bColor = null)
        {
            if (fColor != null)
                Console.ForegroundColor = fColor.Value;

            if (bColor != null)
                Console.BackgroundColor = bColor.Value;

            if (tag != null)
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}][{tag}] {message}");
            }
            else
            {
                Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss}] {message}");
            }

            if (fColor != null)
                Console.ForegroundColor = defFColor;

            if (bColor != null)
                Console.BackgroundColor = defBColor;
        }
    }
}
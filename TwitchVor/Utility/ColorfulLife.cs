using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Utility
{
    // https://github.com/dotnet/runtime/blob/d3ab95d3be895a1950a46c559397780dbb3e9807/src/libraries/Microsoft.Extensions.Logging.Console/src/TextWriterExtensions.cs
    // https://github.com/dotnet/runtime/blob/d3ab95d3be895a1950a46c559397780dbb3e9807/src/libraries/Microsoft.Extensions.Logging.Console/src/AnsiParser.cs

    public static class ColorfulLife
    {
        internal const string DefaultForegroundColor = "\x1B[39m\x1B[22m"; // reset to default foreground color
        internal const string DefaultBackgroundColor = "\x1B[49m"; // reset to the background color

        internal static string GetForegroundColorEscapeCode(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Black => "\x1B[30m",
                ConsoleColor.DarkRed => "\x1B[31m",
                ConsoleColor.DarkGreen => "\x1B[32m",
                ConsoleColor.DarkYellow => "\x1B[33m",
                ConsoleColor.DarkBlue => "\x1B[34m",
                ConsoleColor.DarkMagenta => "\x1B[35m",
                ConsoleColor.DarkCyan => "\x1B[36m",
                ConsoleColor.Gray => "\x1B[37m",
                ConsoleColor.Red => "\x1B[1m\x1B[31m",
                ConsoleColor.Green => "\x1B[1m\x1B[32m",
                ConsoleColor.Yellow => "\x1B[1m\x1B[33m",
                ConsoleColor.Blue => "\x1B[1m\x1B[34m",
                ConsoleColor.Magenta => "\x1B[1m\x1B[35m",
                ConsoleColor.Cyan => "\x1B[1m\x1B[36m",
                ConsoleColor.White => "\x1B[1m\x1B[37m",
                _ => DefaultForegroundColor // default foreground color
            };
        }

        internal static string GetBackgroundColorEscapeCode(ConsoleColor color)
        {
            return color switch
            {
                ConsoleColor.Black => "\x1B[40m",
                ConsoleColor.DarkRed => "\x1B[41m",
                ConsoleColor.DarkGreen => "\x1B[42m",
                ConsoleColor.DarkYellow => "\x1B[43m",
                ConsoleColor.DarkBlue => "\x1B[44m",
                ConsoleColor.DarkMagenta => "\x1B[45m",
                ConsoleColor.DarkCyan => "\x1B[46m",
                ConsoleColor.Gray => "\x1B[47m",
                _ => DefaultBackgroundColor // Use default background color
            };
        }

        public static void WriteColoredMessage(this TextWriter textWriter, string message, ConsoleColor? background = null, ConsoleColor? foreground = null)
        {
            // Order: backgroundcolor, foregroundcolor, Message, reset foregroundcolor, reset backgroundcolor
            if (background.HasValue)
            {
                textWriter.Write(GetBackgroundColorEscapeCode(background.Value));
            }
            if (foreground.HasValue)
            {
                textWriter.Write(GetForegroundColorEscapeCode(foreground.Value));
            }

            textWriter.Write(message);

            if (foreground.HasValue)
            {
                textWriter.Write(DefaultForegroundColor); // reset to default foreground color
            }
            if (background.HasValue)
            {
                textWriter.Write(DefaultBackgroundColor); // reset to the background color
            }
        }

        public static string ColorMe(this string str, ConsoleColor? background = null, ConsoleColor? foreground = null)
        {
            // Этот цирк написан фо фан.
            // Можно мне просто порадоваться?

            if (background != null)
            {
                if (foreground != null)
                    return $"{GetBackgroundColorEscapeCode(background.Value)}{GetForegroundColorEscapeCode(foreground.Value)}{str}{DefaultForegroundColor}{DefaultBackgroundColor}";

                return $"{GetBackgroundColorEscapeCode(background.Value)}{str}{DefaultBackgroundColor}";
            }
            else if (foreground != null)
                return $"{GetForegroundColorEscapeCode(foreground.Value)}{str}{DefaultForegroundColor}";

            return str;
        }
    }
}
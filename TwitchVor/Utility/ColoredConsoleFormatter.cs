using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Pastel;

namespace TwitchVor.Utility
{
    public class ColoredConsoleOptions : ConsoleFormatterOptions
    {
        public class ColoredCategory
        {
            public string Category { get; set; }
            public string? FgColor { get; set; }
            public string? BgColor { get; set; }

            public ColoredCategory(string category, string? fgColor, string? bgColor)
            {
                Category = category;
                FgColor = fgColor;
                BgColor = bgColor;
            }
        }

        public ICollection<ColoredCategory> Colors { get; set; }

        public ColoredConsoleOptions(ICollection<ColoredCategory> colors)
        {
            Colors = colors;
        }
    }

    public class ColoredConsoleFormatter : ConsoleFormatter
    {
        private readonly ColoredConsoleOptions options;

        public ColoredConsoleFormatter(ColoredConsoleOptions options)
            : base("ColoredConsole")
        {
            this.options = options;
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string category = logEntry.Category;
            var colored = options.Colors.FirstOrDefault(c => c.Category == category);

            var (fg, bg) = logEntry.LogLevel switch
            {
                LogLevel.Error => ("#000000", "#FF0000"),
                LogLevel.Warning => ("#FFFF00", "#000000"),
                LogLevel.Information => ("#00FF00", "#000000"),

                _ => ("#FFFFFF", "#000000")
            };

            textWriter.Write(logEntry.LogLevel.ToString()[..4].Pastel(fg).PastelBg(bg));

            textWriter.Write(": ");

            if (colored != null)
            {
                if (colored.FgColor != null)
                    category = category.Pastel(colored.FgColor);
                if (colored.BgColor != null)
                    category = category.PastelBg(colored.BgColor);
            }
            textWriter.Write(category);

            textWriter.Write("[{0}]", logEntry.EventId);
            textWriter.Write("\n      ");

            if (logEntry.Formatter != null)
            {
                textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace TwitchVor.Utility
{
    // https://github.com/dotnet/runtime/blob/d3ab95d3be895a1950a46c559397780dbb3e9807/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs

    public class ColoredConsoleOptions : ConsoleFormatterOptions
    {
        public class ColoredCategory
        {
            public string? Category { get; set; }
            public ConsoleColor? FgColor { get; set; }
            public ConsoleColor? BgColor { get; set; }
        }

        public ICollection<ColoredCategory>? Colors { get; set; }
    }

    public class ColoredConsoleFormatter : ConsoleFormatter, IDisposable
    {
        readonly IDisposable optionsReloadToken;

        private ColoredConsoleOptions options;

        public ColoredConsoleFormatter(IOptionsMonitor<ColoredConsoleOptions> options)
            : base(nameof(ColoredConsoleFormatter))
        {
            optionsReloadToken = options.OnChange(ReloadLoggerOptions);
            this.options = options.CurrentValue;
        }

        private void ReloadLoggerOptions(ColoredConsoleOptions options) => this.options = options;

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
        {
            string category = logEntry.Category;
            var colored = options.Colors?.FirstOrDefault(c => c.Category == category);

            string? timestampFormat = options.TimestampFormat;
            if (timestampFormat != null)
            {
                DateTimeOffset currentDate = GetCurrentDateTime();
                string timestamp = currentDate.ToString(timestampFormat);

                textWriter.Write(timestamp);
            }

            var levelInfo = GetLevelInfo(logEntry.LogLevel);

            textWriter.WriteColoredMessage(levelInfo.text, foreground: levelInfo.fgColor, background: levelInfo.bgColor);

            textWriter.Write(": ");

            if (colored != null)
            {
                category = category.ColorMe(foreground: colored.FgColor, background: colored.BgColor);
            }
            textWriter.Write(category);

            if (logEntry.EventId.Id != 0)
            {
                textWriter.Write("[{0}]", logEntry.EventId);
            }
            
            textWriter.Write("\n      ");

            if (logEntry.Formatter != null)
            {
                textWriter.WriteLine(logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception));
            }
        }

        private DateTimeOffset GetCurrentDateTime()
        {
            return options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        }

        static (string text, ConsoleColor fgColor, ConsoleColor bgColor) GetLevelInfo(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Information => ("info", ConsoleColor.DarkGreen, ConsoleColor.Black),
                LogLevel.Warning => ("warn", ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => ("fail", ConsoleColor.Black, ConsoleColor.DarkRed),
                LogLevel.Critical => ("crit", ConsoleColor.White, ConsoleColor.DarkRed),
                LogLevel.Trace => ("trce", ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Debug => ("dbug", ConsoleColor.Gray, ConsoleColor.Black),

                _ => (logLevel.ToString(), ConsoleColor.White, ConsoleColor.Black)
            };
        }

        public void Dispose()
        {
            optionsReloadToken.Dispose();
        }
    }
}
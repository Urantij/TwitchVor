using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using Pastel;

namespace TwitchVor.Utility
{
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs
    // https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Console/src/AnsiParser.cs

    public class ColoredConsoleOptions : ConsoleFormatterOptions
    {
        public class ColoredCategory
        {
            public string? Category { get; set; }
            public string? FgColor { get; set; }
            public string? BgColor { get; set; }
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

            textWriter.Write(levelInfo.text.Pastel(levelInfo.fgColor).PastelBg(levelInfo.bgColor));

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

        private DateTimeOffset GetCurrentDateTime()
        {
            return options.UseUtcTimestamp ? DateTimeOffset.UtcNow : DateTimeOffset.Now;
        }

        static (string text, string fgColor, string bgColor) GetLevelInfo(LogLevel logLevel)
        {
            return logLevel switch
            {
                LogLevel.Information => ("info", "#008000", "#000000"),
                LogLevel.Warning => ("warn", "#FFFF00", "#000000"),
                LogLevel.Error => ("fail", "#000000", "#800000"),
                LogLevel.Critical => ("crit", "#FFFFFF", "#800000"),
                LogLevel.Trace => ("trce", "#C0C0C0", "#000000"),
                LogLevel.Debug => ("dbug", "#C0C0C0", "#000000"),

                _ => (logLevel.ToString(), "#FFFFFF", "#000000")
            };
        }

        public void Dispose()
        {
            optionsReloadToken.Dispose();
        }
    }
}
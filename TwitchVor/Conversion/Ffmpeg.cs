using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace TwitchVor.Conversion
{
    public partial class Ffmpeg
    {
        static readonly Regex validLastLineRegex = MyRegex();

        readonly ILogger _logger;

        readonly ConversionConfig config;

        public Ffmpeg(ConversionConfig config, ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger(this.GetType());

            this.config = config;
        }

        public ConversionHandler CreateConversion()
        {
            Process process = new();

            process.StartInfo.FileName = config.FfmpegPath;

            process.StartInfo.Arguments = "-f mpegts -i pipe:0 -c copy -f mp4 -movflags isml+frag_keyframe pipe:1";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden; //написано, что должно быть че то тру, а оно фолс. ну похуй, работает и ладно
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            return new ConversionHandler(process);
        }

        public async Task<bool> CheckAsync()
        {
            if (!File.Exists(config.FfmpegPath))
            {
                _logger.LogCritical("Не удаётся найти ффмпег");
                return false;
            }

            using Process process = new();
            process.StartInfo.FileName = config.FfmpegPath;

            process.StartInfo.Arguments = "-version";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
            process.Start();

            string? firstLine = await process.StandardOutput.ReadLineAsync();

            if (firstLine?.Contains("ffmpeg version") != true)
            {
                _logger.LogCritical("Это не ффмпег какой-то.");
                return false;
            }

            _logger.LogInformation("{output}", firstLine);

            await process.StandardOutput.ReadToEndAsync();

            await process.WaitForExitAsync();

            return true;
        }

        public static bool CheckLastLine(string? line)
            => line != null && validLastLineRegex.IsMatch(line);

        [GeneratedRegex(
            "^video:.+?\\saudio:.+?\\ssubtitle:.+?\\sother\\sstreams:.+?\\sglobal\\sheaders:.+?\\smuxing\\soverhead:\\s",
            RegexOptions.Compiled)]
        private static partial Regex MyRegex();
    }
}
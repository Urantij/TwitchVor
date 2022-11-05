using System.Diagnostics;

namespace TwitchVor.Conversion
{
    public class Ffmpeg
    {
        readonly ConversionConfig config;

        public Ffmpeg(ConversionConfig config)
        {
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
    }
}
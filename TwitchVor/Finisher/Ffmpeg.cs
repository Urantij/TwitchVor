using System.Diagnostics;

namespace TwitchVor.Finisher
{
    public static class Ffmpeg
    {
        public static bool Convert(string oldFilePath, string newFilePath)
        {
            using Process pr = new();

            pr.StartInfo.FileName = Program.config.Conversion!.FfmpegPath;
            //pr.StartInfo.Arguments = $"-i \"{oldFilePath}\" -c copy \"{newFilePath}\"";
            //pr.StartInfo.Arguments = $"-i \"{oldFilePath}\" -c copy -movflags faststart \"{newFilePath}\"";

            if (Program.config.Conversion.Arguments != null)
            {
                pr.StartInfo.Arguments = $"-i \"{oldFilePath}\" {Program.config.Conversion.Arguments} \"{newFilePath}\"";
            }
            else
            {
                pr.StartInfo.Arguments = $"-i \"{oldFilePath}\" \"{newFilePath}\"";
            }
            
            pr.StartInfo.UseShellExecute = false;
            pr.StartInfo.RedirectStandardOutput = true;
            pr.StartInfo.RedirectStandardError = true;
            pr.StartInfo.WindowStyle = ProcessWindowStyle.Hidden; //написано, что должно быть че то тру, а оно фолс. ну похуй, работает и ладно
            pr.StartInfo.CreateNoWindow = true;
            pr.Start();

            pr.OutputDataReceived += (s, e) => {};
            pr.ErrorDataReceived += (s, e) => {};

            pr.BeginOutputReadLine();
            pr.BeginErrorReadLine();

            pr.WaitForExit();

            return pr.ExitCode == 0;
        }
    }
}
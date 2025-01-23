using System.Diagnostics;

namespace TwitchVor.Conversion
{
    public class ConversionHandler : IDisposable
    {
        readonly Process process;

        // public int ExitCode => process.ExitCode;

        /// <summary>
        /// Ето читаем
        /// </summary>
        public Stream OutputStream => process.StandardOutput.BaseStream;

        /// <summary>
        /// Его пишем
        /// </summary>
        public Stream InputStream => process.StandardInput.BaseStream;

        public StreamReader TextStream => process.StandardError;

        public ConversionHandler(Process process)
        {
            this.process = process;
        }

        public async Task<int> WaitAsync()
        {
            await process.WaitForExitAsync();

            return process.ExitCode;
        }

        public void Dispose()
        {
            process.Dispose();
        }
    }
}
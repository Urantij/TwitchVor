using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Conversion
{
    public class ConversionHandler : IDisposable
    {
        readonly Process process;

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

        public async Task<bool> WaitAsync()
        {
            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }

        public void Dispose()
        {
            process.Dispose();
        }
    }
}
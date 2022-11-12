using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Utility
{
    public static class StreamExtensions
    {
        /// <summary>
        /// Позволяет передать определённое количество байт.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="output">Туда пишет</param>
        /// <param name="length">Скока байт передать</param>
        /// <returns></returns>
        public static async Task CopyStreamAsync(this Stream input, Stream output, int length)
        {
            //https://stackoverflow.com/a/13022108

            const int bufferSize = 81920; //Дефолтный размер при Stream.CopyTo

            byte[] buffer = new byte[bufferSize];

            int read;
            while (length > 0)
            {
                Memory<byte> memory = buffer.AsMemory(0, Math.Min(bufferSize, length));

                read = await input.ReadAsync(memory);

                if (read == 0)
                    return;

                await output.WriteAsync(memory);
                length -= read;
            }
        }

        //https://stackoverflow.com/a/9958101
        /// <summary>
        /// "Очищает" содержимое, оставляя прежний размер.
        /// </summary>
        /// <param name="source"></param>
        public static void Reset(this MemoryStream source)
        {
            source.Position = 0;
            source.SetLength(0);
        }
    }
}
using System.Diagnostics;

namespace TwitchVor.Conversion;

public class ConversionHandler : IDisposable
{
    private readonly Process process;

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

    /// <summary>
    /// Создаёт таски на чтение текста и чтение с ффмпега, не трогая инпут
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="fromOutputStream"></param>
    /// <param name="textReadAction"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public static Task CreateAsyncProcessing(ConversionHandler handler, Stream fromOutputStream,
        Action<string>? textReadAction = null, CancellationToken cancellation = default)
    {
        Task textTask = Task.Run(async () =>
        {
            while (true)
            {
                string? line = await handler.TextStream.ReadLineAsync(cancellation);

                if (line == null)
                    return;

                textReadAction?.Invoke(line);
            }
        }, cancellation);

        Task readTask = Task.Run(async () =>
        {
            await handler.OutputStream.CopyToAsync(fromOutputStream, cancellation);
            await handler.OutputStream.FlushAsync(cancellation);
        }, cancellation);

        return Task.WhenAll(textTask, readTask);
    }

    /// <summary>
    /// Перенаправляет <see cref="toInputStream"/> в ффмпег, ффмпег в <see cref="fromOutputStream"/>
    /// После чего диспоузит <see cref="handler"/>
    /// </summary>
    /// <param name="handler"></param>
    /// <param name="toInputStream"></param>
    /// <param name="fromOutputStream"></param>
    /// <param name="textReadAction"></param>
    /// <param name="cancellation"></param>
    public static async Task FullProcessAsync(ConversionHandler handler, Stream toInputStream,
        Stream fromOutputStream, Action<string>? textReadAction = null, CancellationToken cancellation = default)
    {
        Task processingTask = CreateAsyncProcessing(handler, fromOutputStream, textReadAction, cancellation);

        await toInputStream.CopyToAsync(handler.InputStream, cancellation);
        await toInputStream.FlushAsync(cancellation);

        await handler.InputStream.FlushAsync(cancellation);
        await handler.InputStream.DisposeAsync();

        await processingTask;

        handler.Dispose();
    }
}
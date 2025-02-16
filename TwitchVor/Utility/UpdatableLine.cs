namespace TwitchVor.Utility;

// Проблема, что хоть методы консоли и зависают, токенов отмены там нет.
// Так что в теории эти строки будут висеть до конца жизни программы, если с инпутом чето не так.
public class UpdatableLine
{
    private readonly int left;
    private readonly int top;

    private readonly CancellationToken cancellationToken;
    private bool stopped = false;

    public UpdatableLine(int left, int top, CancellationToken cancellationToken)
    {
        this.left = left;
        this.top = top;
        this.cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Консоль синхронна. Если вводить что-то, или не нажать инпут после входа программы, то получение позиции курсора будет просто ждать.
    /// </summary>
    /// <returns></returns>
    public Task UpdateAsync(string text)
    {
        return Task.Run(() =>
        {
            var (CurrentLeft, CurrentTop) = Console.GetCursorPosition();

            if (cancellationToken.IsCancellationRequested || stopped)
                return;

            Console.SetCursorPosition(left, top);

            Console.Write(text.PadRight(Console.BufferWidth));

            Console.SetCursorPosition(CurrentLeft, CurrentTop);
        });
    }

    public void Stop()
    {
        stopped = true;
    }

    /// <summary>
    /// Консоль синхронна. Если вводить что-то, или не нажать инпут после входа программы, то получение позиции курсора будет просто ждать.
    /// </summary>
    /// <returns></returns>
    public static Task<UpdatableLine> Create(string text, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            var (Left, Top) = Console.GetCursorPosition();

            System.Console.WriteLine(text);

            return new UpdatableLine(Left, Top, cancellationToken);
        });
    }
}
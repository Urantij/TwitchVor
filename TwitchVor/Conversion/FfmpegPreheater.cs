namespace TwitchVor.Conversion;

// Когда нужно вызывать ффмпег по миллиону раз, возможно стоит прехитить несколько процессов
// сделаю фо фан женерик, но оставлю ффмпег название, хихихи

public class FfmpegPreheater<T>
{
    private readonly int _size;
    private readonly int _expectedCalls;
    private readonly Func<Task<T>> _factory;

    private int _called = 0;

    private readonly Queue<Task<T>> _container;

    public FfmpegPreheater(int size, int expectedCalls, Func<Task<T>> factory)
    {
        _size = size;
        _expectedCalls = expectedCalls;
        _factory = factory;

        _container = new Queue<Task<T>>(size);
    }

    public void Heat()
    {
        int left = _expectedCalls - _called;

        int toFill = Math.Min(_size, left);

        for (int i = 0; i < toFill; i++)
        {
            _container.Enqueue(_factory());
        }
    }

    public Task<T> GetAsync()
    {
        Task<T> result;

        if (_container.Count > 0)
        {
            result = _container.Dequeue();

            _called++;

            if (_called < _expectedCalls)
            {
                _container.Enqueue(_factory());
            }
        }
        else
        {
            result = _factory();

            _called++;

            Heat();
        }

        return result;
    }
}
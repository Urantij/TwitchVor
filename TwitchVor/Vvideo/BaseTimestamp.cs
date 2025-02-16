namespace TwitchVor.Vvideo;

public abstract class BaseTimestamp
{
    /// <summary>
    /// Абсолютный. UTC пожалуйста.
    /// </summary>
    private readonly DateTime _timestamp;

    public TimeSpan? Offset { get; protected set; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="timestamp">Абсолютный. UTC пожалуйста.</param>
    protected BaseTimestamp(DateTime timestamp)
    {
        this._timestamp = timestamp;
    }

    /// <summary>
    /// Без <see cref="Offset"/>
    /// </summary>
    /// <returns></returns>
    public DateTime GetRawTime()
    {
        return _timestamp;
    }

    public DateTime GetTimeWithOffset()
    {
        if (Offset == null)
            return _timestamp;

        return _timestamp + Offset.Value;
    }

    public void SetOffset(TimeSpan offset)
    {
        Offset = offset;
    }

    /// <summary>
    /// Создать строку, которая будет в описании.
    /// </summary>
    /// <returns></returns>
    public abstract string MakeString();
}
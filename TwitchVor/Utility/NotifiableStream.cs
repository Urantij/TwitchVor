using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Utility;

public class NotifiableStream : Stream
{
    private readonly Stream inner;

    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override bool CanTimeout => inner.CanTimeout;

    public override int ReadTimeout
    {
        get => inner.ReadTimeout;
        set => inner.ReadTimeout = value;
    }

    public override int WriteTimeout
    {
        get => inner.WriteTimeout;
        set => inner.WriteTimeout = value;
    }

    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public bool WasReaded { get; private set; }
    public bool WasWritten { get; private set; }

    public event Action? FirstReaded;
    public event Action? FirstWritten;

    public NotifiableStream(Stream inner)
    {
        this.inner = inner;
    }

    public override void Flush()
    {
        inner.Flush();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return inner.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        inner.SetLength(value);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (!WasReaded)
        {
            WasReaded = true;
            FirstReaded?.Invoke();
        }

        return inner.Read(buffer, offset, count);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (!WasWritten)
        {
            WasWritten = true;
            FirstWritten?.Invoke();
        }

        inner.Write(buffer, offset, count);
    }

    public override void Close()
    {
        base.Close();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
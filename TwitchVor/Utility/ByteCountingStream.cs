using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TwitchVor.Utility;

// https://stackoverflow.com/a/34275894

/// <summary>
/// A wrapper around a <see cref="Stream"/> that keeps track of the number of bytes read and written.
/// </summary>
public class ByteCountingStream : Stream
{
    // CopyTo использует Write
    // FlushAsync вызывает просто Flush
    // Скорее всего, DisposeAsync тоже вызывает обычную версию, но его в сурсах нет.
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

    public long TotalBytesWritten { get; private set; }
    public long TotalBytesRead { get; private set; }

    public ByteCountingStream(Stream inner)
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
        int readBytes = inner.Read(buffer, offset, count);
        TotalBytesRead += readBytes;
        return readBytes;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        inner.Write(buffer, offset, count);
        TotalBytesWritten += count;
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
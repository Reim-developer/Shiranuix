namespace Shiranuix.Services;

/// <summary>
/// A view of one stretch of a stream: everything before the window's start
/// and everything after its end is invisible, and the stream's own end is
/// where the window's ends.
/// </summary>
/// <remarks>
/// This is how a <c>.shiranux</c> file is read without decoding it whole.
/// The file keeps its text between a header and the alignments that follow,
/// and a reader handed the file itself would read the binary beside the
/// letters as if it were letters. Bounding the stream to the text's own
/// bytes means every reader here — the scan, the walks, the window — sees
/// only the text, and reaches the end of the text rather than the end of
/// the file. Positions are counted from the window's own start, so an
/// anchor taken inside the window reads back the same wherever the window
/// stands in the file.
/// </remarks>
internal sealed class BoundedStream : Stream {
    private readonly Stream _inner;
    private readonly long _origin;
    private readonly long _length;

    /// <summary>
    /// Wraps a stream, keeping only the stretch from an origin for a
    /// length.
    /// </summary>
    /// <param name="inner">The stream to read through.</param>
    /// <param name="origin">Where the window starts, in the inner stream.</param>
    /// <param name="length">How many bytes the window holds.</param>
    public BoundedStream(Stream inner, long origin, long length) {
        ArgumentNullException.ThrowIfNull(inner);

        ArgumentOutOfRangeException.ThrowIfNegative(origin);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        _inner = inner;
        _origin = origin;
        _length = length;
    }

    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => true;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => _length;

    /// <inheritdoc />
    /// <remarks>
    /// Counted from the window's own start, not the file's, so a byte the
    /// walk measures inside the window means the same thing here as it
    /// does everywhere else in the app.
    /// </remarks>
    public override long Position {
        get => _inner.Position - _origin;
        set => _inner.Position = _origin + Math.Clamp(value, 0, _length);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The end is the window's end, not the stream's: a walk that runs off
    /// the text stops at the text, where the alignments begin.
    /// </remarks>
    public override long Seek(long offset, SeekOrigin origin) {
        Position = origin switch {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => _length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, null)
        };

        return Position;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Never reads past the window even if asked to: what lies beyond the
    /// text is another section's bytes, and a reader that trusted them
    /// would hand the walker a binary that looks like letters.
    /// </remarks>
    public override int Read(byte[] buffer, int offset, int count) {
        var remaining = _length - Position;

        if (remaining <= 0) {
            return 0;
        }

        return _inner.Read(buffer, offset, (int)Math.Min(count, remaining));
    }

    /// <inheritdoc />
    /// <inheritdoc cref="Read(byte[], int, int)" path="/remarks" />
    public override int Read(Span<byte> buffer) {
        var remaining = _length - Position;

        if (remaining <= 0) {
            return 0;
        }

        return _inner.Read(buffer[..(int)Math.Min(buffer.Length, remaining)]);
    }

    /// <inheritdoc />
    public override void Flush() {
    }

    /// <inheritdoc />
    public override void SetLength(long value) =>
        throw new NotSupportedException("A window onto a file does not decide how long the file is.");

    /// <inheritdoc />
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException("A window onto a file is read only.");

    /// <inheritdoc />
    /// <remarks>
    /// Closing the window closes the file it reads through: every window
    /// here owns the stream it wrapped, and leaves nothing open behind it.
    /// </remarks>
    protected override void Dispose(bool disposing) {
        if (disposing) {
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }
}

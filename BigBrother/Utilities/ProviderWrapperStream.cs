using NAudio.Wave;

namespace BigBrother.Utilities;

public class ProviderWrapperStream(IWaveProvider provider) : Stream
{
    public override void Flush()
    {
        throw new InvalidOperationException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return provider.Read(buffer, offset, count);
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new InvalidOperationException();
    }

    public override void SetLength(long value)
    {
        throw new InvalidOperationException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new InvalidOperationException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new InvalidOperationException();
    public override long Position
    {
        get => throw new InvalidOperationException();
        set => throw new InvalidOperationException();
    }
}

public static class ProviderWrapperStreamExtensions
{
    public static Stream ToStream(this IWaveProvider provider)
        => new ProviderWrapperStream(provider);
}
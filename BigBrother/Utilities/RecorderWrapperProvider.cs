using NAudio.Wave;

namespace BigBrother.Utilities;

public class RecorderWrapperProvider(IWaveProvider provider) : Stream, IWaveProvider
{
    public bool IsRecording => _record != null;

    private (TimeSpan Timestamp, MemoryStream Stream)? _record;

    public WaveFormat WaveFormat => provider.WaveFormat;

    public override int Read(byte[] buffer, int offset, int count)
    {
        count = provider.Read(buffer, offset, count);
        _record?.Stream.Write(buffer, offset, count);
        return count;
    }

    public void StartRecording(TimeSpan timestamp)
    {
        if (_record != null)
            return;
        _record = (timestamp, new MemoryStream());
    }

    public (TimeSpan Duration, MemoryStream Wave) StopRecording(TimeSpan timestamp)
    {
        if (_record == null)
            throw new InvalidOperationException("This Recorder Wrapper is not recording.");
        var record = _record.Value;
        _record = null;
        record.Stream.Seek(0, SeekOrigin.Begin);
        return (timestamp - record.Timestamp, record.Stream);
    }

    public override void Flush()
    {
        throw new InvalidOperationException();
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
    public override long Position { 
        get => throw new InvalidOperationException();
        set => throw new InvalidOperationException();
    }
}
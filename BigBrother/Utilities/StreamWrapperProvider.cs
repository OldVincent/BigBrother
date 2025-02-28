using NAudio.Wave;

namespace BigBrother.Utilities;

public class StreamWrapperProvider(Stream stream, WaveFormat format) : IWaveProvider
{
    public int Read(byte[] buffer, int offset, int count)
        => stream.Read(buffer, offset, count);

    public WaveFormat WaveFormat => format;
}
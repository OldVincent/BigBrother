using System.Buffers;
using NAudio.Wave;

namespace BigBrother.Devices;

public class SpeakerDevice : IAgentAudioOutput, IDisposable
{
    private readonly WaveOutEvent _output;
    
    private readonly BufferedWaveProvider _provider;

    public SpeakerDevice(int deviceNumber = 0)
    {
        var format = new WaveFormat(24000, 16, 1);
        _output = new WaveOutEvent()
        {
            DeviceNumber = deviceNumber
        };
        _provider = new BufferedWaveProvider(format)
        {
            BufferDuration = TimeSpan.FromMinutes(5)
        };
        _output.Init(_provider);
        _output.Play();
    }

    public void Write(ReadOnlyMemory<byte> data)
    {
        var span = data.Span;
        var buffer = ArrayPool<byte>.Shared.Rent(span.Length);
        span.CopyTo(buffer.AsSpan());
        _provider.AddSamples(buffer, 0, span.Length);
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public void Clear()
    {
        _provider.ClearBuffer();
    }

    public void Dispose()
    {
        _provider.ClearBuffer();
        _output.Stop();
        _output.Dispose();
    }
}
using NAudio.Utils;
using NAudio.Wave;

namespace BigBrother.Devices;

public class MicrophoneDevice : IAgentAudioInput, IDisposable
{
    public WaveFormat WaveFormat { get; }

    private readonly WaveInEvent _input;

    private readonly CircularBuffer _buffer;

    public MicrophoneDevice(int deviceNumber = 0)
    {
        WaveFormat = new WaveFormat(24000, 16, 1);

        _buffer = new CircularBuffer(WaveFormat.AverageBytesPerSecond * 60);
        _input = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = WaveFormat
        };
        _input.DataAvailable += (_, arguments) =>
        {
            _buffer.Write(arguments.Buffer, 0, arguments.BytesRecorded);
        };
    }

    public void Start()
    {
        _input.StartRecording();
    }

    public void Stop()
    {
        _input.StopRecording();
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return _buffer.Read(buffer, offset, count);
    }

    public void Dispose()
    {
        _input.Dispose();
        _buffer.Reset();
    }
}
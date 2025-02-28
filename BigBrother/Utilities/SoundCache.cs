using System.Buffers;
using NAudio.Wave;

namespace BigBrother.Utilities;

public class SoundCache
{
    private readonly Dictionary<string, byte[]> _cache = new();

    public void Clear()
    {
        _cache.Clear();
    }
    
    public byte[] Load(string path)
    {
        if (_cache.TryGetValue(path, out var audio)) 
            return audio;
        var stream = new MemoryStream();
        var reader = new WaveFileReader(path);
        var buffer = ArrayPool<byte>.Shared.Rent(reader.WaveFormat.AverageBytesPerSecond);
        while (true)
        {
            var count = reader.Read(buffer, 0, buffer.Length);
            if (count == 0)
                break;
            stream.Write(buffer, 0, count);
        }
        ArrayPool<byte>.Shared.Return(buffer);
        audio = stream.ToArray();
        _cache[path] = audio;
        return audio;
    }
}
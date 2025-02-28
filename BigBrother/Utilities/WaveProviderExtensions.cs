using System.Buffers;
using NAudio.Wave;

namespace BigBrother.Utilities;

public static class WaveProviderExtensions
{
    public static MemoryStream ToWaveFileStream(this IWaveProvider provider, WaveFormat format)
    {
        var stream = new MemoryStream();

        var buffer = ArrayPool<byte>.Shared.Rent(format.AverageBytesPerSecond);

        var writer = new WaveFileWriter(stream, format);

        if (provider is BufferedWaveProvider bufferedProvider)
        {
            while (bufferedProvider.BufferedBytes > 0)
            {
                var count = Math.Min(buffer.Length, bufferedProvider.BufferedBytes);
                bufferedProvider.Read(buffer, 0, count);
                writer.Write(buffer, 0, count);
            }
        }
        else
        {
            while (true)
            {
                var read = provider.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    break;
                writer.Write(buffer, 0, read);
            }
        }
        
        ArrayPool<byte>.Shared.Return(buffer);

        writer.Flush();
        
        stream.Seek(0, SeekOrigin.Begin);

        return stream;
    }
    
    public static WaveFormatConversionProvider ToTargetFormat(this IWaveProvider provider, WaveFormat targetFormat)
        => new (targetFormat, provider);
}
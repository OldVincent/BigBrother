using BigBrother.Services;
using BigBrother.Utilities;
using Google.Protobuf;
using Jsk.Services;
using MongoDB.Bson;
using MongoDB.Driver;
using NAudio.Wave;
using OpenCvSharp;
using Spectre.Console;

namespace BigBrother.Commands;

public static class DetectUserByVoiceCommand
{
    public static async Task Run(UserManagementService service)
    {
        var audioFormat = new WaveFormat(48000, 16, 1);
        var microphone = new WaveInEvent()
        {
            WaveFormat = audioFormat,
        };

        var audioBuffer = new BufferedWaveProvider(audioFormat)
        {
            BufferDuration = TimeSpan.FromMinutes(1),
            ReadFully = false
        };
        microphone.DataAvailable += (_, data) => { audioBuffer.AddSamples(data.Buffer, 0, data.BytesRecorded); };
        microphone.StartRecording();


        while (!await AnsiConsole.ConfirmAsync("Have you read something loud to the console?"))
        {
            AnsiConsole.Write("You must read something loud to continue.", Color.Red);
        }

        microphone.StopRecording();

        var stream = new WaveFormatConversionProvider(
            new WaveFormat(48000, 16, 1),
            audioBuffer
                .ToSampleProvider()
                .Take(TimeSpan.FromSeconds(40))
                .ToWaveProvider16()
        ).ToWaveFileStream(new WaveFormat(48000, 16, 1));
        
        var embedding = await service.GetUserVoiceEmbedding(stream.ToArray());

        var users = await service.SearchUsersByVoice(embedding, 3, .0)
            .ToListAsync();

        Console.WriteLine(users.ToJson());
    }
}
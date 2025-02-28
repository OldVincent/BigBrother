using BigBrother.Services;
using BigBrother.Utilities;
using Google.Protobuf;
using Jsk.Services;
using NAudio.Wave;
using OpenCvSharp;
using Spectre.Console;

namespace BigBrother.Commands;

public static class CreateUserCommand
{
    public static async Task Run(
        AudioIdentificationService.AudioIdentificationServiceClient audioIdentifier,
        FaceIdentificationService.FaceIdentificationServiceClient faceIdentifier,
        UserManagementService service
    )
    {
        AnsiConsole.Write(new Rule("Creating User")
        {
            Justification = Justify.Center,
            Style = Color.Aqua
        });

        AnsiConsole.Write(new Rule("Information")
        {
            Justification = Justify.Left,
            Style = Color.Green
        });

        var document = new UserDocument();

        document.Information = new UserDocument.InformationDocument
        {
            FullName = AnsiConsole.Ask<string>("What is the full name of the user?"),
            CalledAs = AnsiConsole.Ask<string>(
                "How would you like to be called as when the agent is talking to you?"),
            ReferedAs = AnsiConsole.Ask<string>(
                "How would you like to be referred as when the agent is talking to others?"),
            NickName = AnsiConsole.Ask<string>("What is your nickname?")
        };

        AnsiConsole.Write(new Rule("Biometrics")
        {
            Justification = Justify.Left,
            Style = Color.Green
        });

        AnsiConsole.Write(
            new Text("We need to take a photo of your face. Please enter to continue after the window shows.",
                Color.Orange1));

        var camera = new VideoCapture(0);
        var face = new Mat();
        while (Cv2.WaitKey(1) != 27)
        {
            while (!camera.Read(face))
            {
                await Task.Delay(100);
            }

            Cv2.ImShow("Face - Press ESC to Continue", face);
        }
        Cv2.DestroyWindow("Face - Press ESC to Continue");
        Cv2.WaitKey(1);

        document.Biometrics.Face = faceIdentifier.Identify(
            new FaceIdentificationRequest
            {
                Image = ByteString.CopyFrom(face.ImEncode(".jpg"))
            }).Faces[0].Embedding.ToArray();

        AnsiConsole.Write(
            new Text("We need to take a record of your voice. Please read the following text loud to the microphone.",
                Color.Orange1));

        AnsiConsole.WriteLine();

        var microphone = new WaveInEvent();
        var audioFormat = new WaveFormat(48000, 16, 1);
        var audioBuffer = new BufferedWaveProvider(audioFormat)
        {
            BufferDuration = TimeSpan.FromMinutes(1),
            ReadFully = false
        };
        microphone.DataAvailable += (_, data) => { audioBuffer.AddSamples(data.Buffer, 0, data.BytesRecorded); };
        microphone.StartRecording();

        AnsiConsole.Write(
            new Panel(
                "By using Big Brother, I acknowledge that I am solely responsible for my interactions " +
                "with this intelligent robot agent developed by JSK Robotics Laboratory, " +
                "The University of Tokyo. " +
                "I understand that neither the University of Tokyo nor JSK Robotics Laboratory will be liable for " +
                "any damages, losses, or consequences resulting from my use of Big Brother. " +
                "I agree to use the system responsibly, " +
                "exercising caution and ensuring compliance with applicable laws and ethical considerations. " +
                "By continuing to use Big Brother, I accept these terms."
            )
            {
                BorderStyle = Color.Orange1
            });

        while (!await AnsiConsole.ConfirmAsync("Have you read the whole text and agreed to the terms?"))
        {
            AnsiConsole.Write("You must agree to the terms to continue.", Color.Red);
        }

        microphone.StopRecording();

        var slicedBuffer = audioBuffer
            .ToSampleProvider()
            .Take(TimeSpan.FromSeconds(30))
            .ToWaveProvider16()
            .ToWaveFileStream(audioBuffer.WaveFormat);

        document.Biometrics.Voice = audioIdentifier.Identify(
            new AudioIdentificationRequest
            {
                Audio = ByteString.CopyFrom(
                    slicedBuffer.ToArray())
            }).Embedding.ToArray();

        await service.CreateUser(document);

        AnsiConsole.Write(new Text("Account has been created. Thank you for choosing Big Brother.", Color.Green));
    }
}
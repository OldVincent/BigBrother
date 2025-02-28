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

public static class DetectUserByFaceCommand
{
    public static async Task Run(
        FaceIdentificationService.FaceIdentificationServiceClient faceIdentifier,
        UserManagementService service)
    {
        var camera = new VideoCapture(0);
        var face = new Mat();
        while (Cv2.WaitKey(1) != 27)
        {
            while (!camera.Read(face))
            {
                await Task.Delay(100);
            }
            Cv2.ImShow("Face - Press Enter to Continue", face);
        }
        
        var embedding = faceIdentifier.Identify(
            new FaceIdentificationRequest
            {
                Image = ByteString.CopyFrom(face.ImEncode(".jpg"))
            }).Faces[0].Embedding.ToArray();

        var user = await service.SearchUsersByFace(embedding, 1).FirstAsync();
        
        Console.Write(user.ToJson());
    }
}
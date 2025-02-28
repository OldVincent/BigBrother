using Google.Protobuf;
using Jsk.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BigBrother.Services;

[BsonIgnoreExtraElements]
public class UserDocument
{
    [BsonIgnoreExtraElements]
    public class BiometricsDocument
    {
        public float[] Voice { get; set; }

        public float[] Face { get; set; }
    }

    [BsonIgnoreExtraElements]
    public class InformationDocument
    {
        public string FullName { get; set; }

        public string NickName { get; set; }

        public string CalledAs { get; set; }

        public string ReferedAs { get; set; }
    }

    [BsonRepresentation(BsonType.String)] public Guid Identifier { get; set; } = Guid.NewGuid();
    public BiometricsDocument Biometrics { get; set; } = new();

    public InformationDocument Information { get; set; } = new();

    public Dictionary<string, string> Permissions { get; set; } = [];

    public List<string> Notes { get; set; } = [];

    public HashSet<string> Roles { get; set; } = [];
}

public class UserManagementService(
    IMongoCollection<UserDocument> collection,
    AudioIdentificationService.AudioIdentificationServiceClient audioIdentifier,
    FaceIdentificationService.FaceIdentificationServiceClient faceIdentifier)
{
    public async Task CreateUser(UserDocument profile)
    {
        await collection.InsertOneAsync(profile);
    }

    public async Task<UserDocument> SearchUserById(Guid identifier)
    {
        return await (await collection.FindAsync(document => document.Identifier == identifier))
            .FirstAsync();
    }

    public async Task<float[]> GetUserVoiceEmbedding(Memory<byte> audio)
    {
        return (await audioIdentifier.IdentifyAsync(
            new AudioIdentificationRequest()
            {
                Audio = ByteString.CopyFrom(audio.Span)
            })).Embedding.ToArray();
    }

    public async IAsyncEnumerable<(UserDocument Profile, double Similarity)> SearchUsersByVoice(
        float[] embedding, int limit = 1,
        double similarityThreshold = 0.70)
    {
        var cursor = await collection.Aggregate()
            .VectorSearch(
                document => document.Biometrics.Voice,
                new QueryVector(embedding), limit,
                new VectorSearchOptions<UserDocument>
                {
                    Exact = true,
                    IndexName = "biometrics-voice",
                })
            .Project(
                Builders<UserDocument>.Projection.MetaVectorSearchScore("Similarity")
                    .Include(nameof(UserDocument.Identifier))
                    .Include(nameof(UserDocument.Information))
                    .Include(nameof(UserDocument.Permissions))
                    .Include(nameof(UserDocument.Notes)))
            .Match(document => document["Similarity"].AsDouble >= similarityThreshold)
            .SortByDescending(document => document["Similarity"])
            .ToCursorAsync();
        
        while (await cursor.MoveNextAsync())
        {
            foreach (var document in cursor.Current)
            {
                yield return (BsonSerializer.Deserialize<UserDocument>(document),
                    document["Similarity"].AsDouble);
            }
        }
    }


    public async Task<float[]> GetUserFaceEmbedding(Memory<byte> image)
    {
        return (await faceIdentifier.IdentifyAsync(
            new FaceIdentificationRequest()
            {
                Image = ByteString.CopyFrom(image.Span)
            })).Faces.First().Embedding.ToArray();
    }

    public async IAsyncEnumerable<(UserDocument Profile, double Similarity)> SearchUsersByFace(
        float[] embedding, int limit = 1,
        double similarityThreshold = 0.89)
    {
        var cursor = await collection.Aggregate()
            .VectorSearch(
                document => document.Biometrics.Face,
                new QueryVector(embedding), limit, new VectorSearchOptions<UserDocument>()
                {
                    Exact = true,
                    IndexName = "biometrics-face",
                })
            .Project(
                Builders<UserDocument>.Projection
                    .MetaVectorSearchScore("Similarity")
                    .Include(nameof(UserDocument.Identifier))
                    .Include(nameof(UserDocument.Information))
                    .Include(nameof(UserDocument.Permissions))
                    .Include(nameof(UserDocument.Notes)))
            .Match(document => document["Similarity"].AsDouble >= similarityThreshold)
            .SortByDescending(document => document["Similarity"])
            .ToCursorAsync();

        while (await cursor.MoveNextAsync())
        {
            foreach (var document in cursor.Current)
            {
                yield return (BsonSerializer.Deserialize<UserDocument>(document),
                    document["Similarity"].AsDouble);
            }
        }
    }
}

public static class UserManagementServiceExtensions
{
    public static IServiceCollection AddUserManagementService(
        this IServiceCollection services, 
        string collection)
    {
        services.AddSingleton<UserManagementService>(provider => 
            new UserManagementService(
                provider.GetRequiredService<IMongoDatabase>().GetCollection<UserDocument>(collection),
                provider.GetRequiredService<AudioIdentificationService.AudioIdentificationServiceClient>(),
                provider.GetRequiredService<FaceIdentificationService.FaceIdentificationServiceClient>()
            ));
        return services;
    }
}
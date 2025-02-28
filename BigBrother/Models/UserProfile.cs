using MongoDB.Bson.Serialization.Attributes;

namespace BigBrother.Models;

public class UserProfile
{
    [BsonIgnore] public Guid Identifier => Information.Identifier;
    
    [BsonIgnore] public string FullName => Information.FullName;
    
    public UserInformation Information { get; set; } = new();

    public Dictionary<string, string> Permissions { get; set; } = new();

    public List<string> Notes { get; set; } = new();
}
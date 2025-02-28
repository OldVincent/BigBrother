using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BigBrother.Models;

[BsonIgnoreExtraElements]
public class UserInformation
{
    [BsonRepresentation(BsonType.String)]
    public Guid Identifier { get; set; } = Guid.NewGuid();
    
    public string FullName { get; set; } = "";

    public string NickName { get; set; } = "";
    
    public string CalledAs { get; set; } = "";

    public string ReferredAs { get; set; } = "";
}
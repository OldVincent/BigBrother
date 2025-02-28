using Jsk.Services;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace BigBrother.Services;

[BsonIgnoreExtraElements]
public class ActionDocument
{
    /// <summary>
    /// Brief description of this action.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// The actual content of this action for the agent to perform.
    /// </summary>
    public string Content { get; set; } = "";
    
    /// <summary>
    /// Roles allowed to perform this action.
    /// </summary>
    [BsonDefaultValue(null)]
    public HashSet<string>? AllowedRoles { get; set; } = null;

    /// <summary>
    /// Roles disallowed to perform this action.
    /// </summary>
    [BsonDefaultValue(null)]
    public HashSet<string>? DisallowedRoles { get; set; } = null;

    public List<string> Conditions { get; set; } = [];
}

public class ActionManagementService(IMongoCollection<ActionDocument> collection)
{
    public Task CreateAction(ActionDocument action)
    {
        return collection.InsertOneAsync(action);
    }
    
    public IAsyncEnumerable<ActionDocument> FilterActions(HashSet<string> roles)
    {
        return collection.AsQueryable()
            .Where(action => (action.AllowedRoles == null || action.AllowedRoles.Intersect(roles).Any()) && 
                             (action.DisallowedRoles == null || !action.DisallowedRoles.Intersect(roles).Any()))
            .ToAsyncEnumerable();
    }
}

public static class ActionManagementServiceExtensions
{
    public static IServiceCollection AddActionManagementService(
        this IServiceCollection services, string collection)
    {
        services.AddSingleton<ActionManagementService>(provider => 
            new ActionManagementService(
                provider.GetRequiredService<IMongoDatabase>().GetCollection<ActionDocument>(collection)
            ));
        return services;
    }
}
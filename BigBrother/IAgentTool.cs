using System.Diagnostics.CodeAnalysis;
using MongoDB.Bson;
using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002

namespace BigBrother;

public abstract class AgentTool
{
    public abstract string Name { get; }
    
    public abstract string Description { get; }
    
    public abstract BsonDocument Schema { get; }

    [field: MaybeNull]
    internal ConversationTool Descriptor => field ??= ConversationTool.CreateFunctionTool(
        Name, Description, BinaryData.FromString(Schema.ToJson()));
    
    public abstract Task<string> Handle(string arguments);
}

public class LambdaAgentTool(
    string name, 
    string description, 
    BsonDocument schema,
    Func<string, Task<string>> handler) : AgentTool
{
    public override string Name => name;
    
    public override string Description => description;
    
    public override BsonDocument Schema => schema;
    
    public override Task<string> Handle(string arguments)
    {
        return handler(arguments);
    }
}
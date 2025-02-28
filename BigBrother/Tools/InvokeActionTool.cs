using System.Text;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;

#pragma warning disable OPENAI002

namespace BigBrother.Tools;

public class InvokeActionTool(ILogger logger) : AgentTool
{
    public override string Name => "PerformAction";
    public override string Description => "Use this tool to send an Action request to an Action Server.";

    public override BsonDocument Schema { get; } =
        BsonDocument.Parse("""
                           {
                             "type": "object",
                             "properties": {
                               "server": {
                                 "type": "string",
                                 "description": "The URL of the Action Server."
                               },
                               "content": {
                                 "type": "string",
                                 "description": "The content of the request to send to the Action Server."
                               }
                             },
                             "required": ["server", "content"]
                           }
                           """);

    public override async Task<string> Handle(string data)
    {
        var document = BsonDocument.Parse(data);
        var server = document["server"].AsString;
        var content = document["content"].AsString;

        using var scope = logger.BeginScope("Action Request to Server {Server}", server);
        logger.LogInformation("Sending Action request to Action Server {Server}, content: {Content}",
            server, content);

        var client = new HttpClient();
        var response = await client.PostAsync(server,
            new StringContent(content, Encoding.UTF8, "application/json"));
        var result = await response.Content.ReadAsStringAsync();

        logger.LogInformation("Received response from the Action Server {Server}, content: {Content}",
            server, result);


        return result;
    }
}
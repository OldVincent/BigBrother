using System.ClientModel;
using System.CommandLine;
using Azure.AI.OpenAI;
using BigBrother.Commands;
using BigBrother.Services;
using Grpc.Net.Client;
using Jsk.Services;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using OpenAI.RealtimeConversation;

#pragma warning disable OPENAI002

namespace BigBrother;

public class Launcher
{
    public static async Task Main(string[] arguments)
    {
        var services = new ServiceCollection();
        services.AddLogging(options =>
        {
            options.AddSimpleConsole(logging =>
            {
                logging.TimestampFormat = "[HH:mm:ss.ff] ";
            });
        });
        services.AddBigBrotherAzureResources();
        services.AddTransient<FaceIdentificationService.FaceIdentificationServiceClient>(_ =>
        {
            var channel = GrpcChannel.ForAddress("http://dlbox13:50000");
            return new FaceIdentificationService.FaceIdentificationServiceClient(channel);
        });
        services.AddTransient<AudioIdentificationService.AudioIdentificationServiceClient>(_ =>
        {
            var channel = GrpcChannel.ForAddress("http://dlbox13:50001");
            return new AudioIdentificationService.AudioIdentificationServiceClient(channel);
        });
        services.AddSingleton<MongoClient>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return new MongoClient(configuration["MongoDB:ConnectionString"]);
        });
        services.AddSingleton<IMongoDatabase>(
            provider => provider.GetRequiredService<MongoClient>().GetDatabase("BigBrother"));

        services.AddUserManagementService("Users");
        services.AddActionManagementService("Actions");

        var provider = services.BuildServiceProvider();
        
        await RunAgentCommand.Run(provider);
        
        var commandRoot = new RootCommand("Big Brother");
        commandRoot.SetHandler(async () => { await RunAgentCommand.Run(provider); });
        
        var commandCreateUser = new Command("create-user", "Create a new user");
        commandCreateUser.SetHandler(async () =>
        {
            await CreateUserCommand.Run(
                provider.GetRequiredService<AudioIdentificationService.AudioIdentificationServiceClient>(),
                provider.GetRequiredService<FaceIdentificationService.FaceIdentificationServiceClient>(),
                provider.GetRequiredService<UserManagementService>()
            );
        });
        commandRoot.Add(commandCreateUser);
        
        
        var commandCreateAction = new Command("create-action", "Create a new action");
        commandCreateAction.SetHandler(async () =>
        {
            await CreateActionCommand.Run(provider.GetRequiredService<ActionManagementService>());
        });
        commandRoot.Add(commandCreateAction);
        
        await commandRoot.InvokeAsync(arguments);
    }
}

public static class ResourceExtensions
{
    public static IServiceCollection AddBigBrotherAzureResources(this IServiceCollection services)
    {
        services.AddSingleton<IConfiguration>(_ =>
        {
            var builder = new ConfigurationBuilder();
            builder.AddAzureAppConfiguration(
                "Endpoint=https://bigbrother-config.azconfig.io;" +
                "Id=173T;" +
                "Secret=4Nbtctu5FXeNUKmf2P0zJUs18qdbcVhlRji76PkA6d3CTvgzZrefJQQJ99BBACi0881T1kRtAAACAZAC1Oxf");
            return builder.Build();
        });
        services.AddSingleton<AzureOpenAIClient>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return new AzureOpenAIClient(
                new Uri(configuration["AzureOpenAI:Endpoint"]!),
                new ApiKeyCredential(configuration["AzureOpenAI:Key"]!));
        });
        services.AddTransient<RealtimeConversationClient>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            var client = provider.GetRequiredService<AzureOpenAIClient>();
            return client.GetRealtimeConversationClient(configuration["AzureOpenAI:Model"]!);
        });
        services.AddSingleton<SpeechConfig>(provider =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();
            return SpeechConfig.FromSubscription(
                configuration["AzureSpeech:Key"]!,
                configuration["AzureSpeech:Region"]!);
        });
        return services;
    }
}
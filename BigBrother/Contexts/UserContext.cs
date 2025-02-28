using System.Text;
using BigBrother.Services;
using BigBrother.Tools;
using BigBrother.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using OpenAI.RealtimeConversation;
#pragma warning disable OPENAI002

namespace BigBrother.Contexts;

public class UserContext(
    ILogger<UserContext> logger,
    UserManagementService userManagementService) : AgentContext
{
    public TimeSpan AuthenticationExpiration { get; set; } = TimeSpan.FromMinutes(1);

    public UserDocument? CurrentUser => _authentication?.User;
    
    private (UserDocument User, DateTime Timestamp)? _authentication = null;

    public override async Task<string> OnUserSpeechEnd(TimeSpan endingTime, string itemId,
        TimeSpan speechDuration, MemoryStream speechWave)
    {
        UpdateAuthentication();

        var oldUser = _authentication?.User;
        UserDocument? newUser = null;
        
        if (speechDuration < TimeSpan.FromMilliseconds(800))
        {
            logger.LogInformation(
                "Speech duration {Duration} is too short, user identification is skipped.", speechDuration);
        }
        else
        {
            var audio = new StreamWrapperProvider(speechWave, new WaveFormat(24000, 16, 1));
            var speech =
                speechDuration < TimeSpan.FromSeconds(15)
                    ? audio.ToWaveFileStream(audio.WaveFormat)
                    : audio.ToSampleProvider()
                        .Skip((speechDuration - TimeSpan.FromSeconds(15)) / 2)
                        .Take(TimeSpan.FromSeconds(15))
                        .ToWaveProvider16()
                        .ToWaveFileStream(audio.WaveFormat);
            var embedding = await userManagementService.GetUserVoiceEmbedding(speech.ToArray());
            var user = await userManagementService.SearchUsersByVoice(embedding, 1, 0.6)
                .FirstOrDefaultAsync();
            newUser = user.Profile;
            if (user == default)
            {
                logger.LogInformation("Cannot identify the speaking user from this audio.");
            }
            else
            {
                logger.LogInformation("User identified as {User} with a similarity of {Similarity}.",
                    user.Profile.Information.FullName, user.Similarity);
            }
        }

        var instructions = new StringBuilder();
        
        if (newUser != null)
        {
            // Update the user.
            _authentication = (newUser, DateTime.Now);
        }
        
        logger.LogInformation("Current user in use is {User}.",
            _authentication?.User.Information.FullName ?? "Anonymous");

        if (_authentication != null)
        {
            BuildInstructionsForIdentifiedUser(instructions, _authentication.Value.User);
        }
        else
        {
            BuildInstructionsForAnonymousUser(instructions);
        }
        
        // User has changed.
        if (newUser != null && oldUser?.Identifier != newUser.Identifier)
        {
            if (_authentication != null)
            {
                logger.LogInformation("User changed from {OldUser} to {NewUser}.",
                    _authentication.Value.User.Information.FullName,
                    newUser.Information.FullName);
            }
            else
            {
                logger.LogInformation("User authenticated as {NewUser}.",
                    newUser.Information.FullName);
            }

            instructions.Append("Say hi to this user.");
        }
        
        return instructions.ToString();
    }

    private void UpdateAuthentication()
    {
        if (_authentication == null ||
            DateTime.Now - _authentication.Value.Timestamp <= AuthenticationExpiration) 
            return;
        logger.LogInformation("User authentication of {User} expires.",
            _authentication.Value.User.Information.FullName);
        _authentication = null;
    }
    
    private void BuildInstructionsForAnonymousUser(StringBuilder builder)
    {
        builder.AppendLine("The system cannot identify the identity of this user.");
    }
    
    private void BuildInstructionsForIdentifiedUser(StringBuilder builder, UserDocument user)
    {
        builder.AppendLine(
            $"""
             The currently speaking user is {user.Information.FullName},
             you should call this user as {user.Information.CalledAs};
             other human users may also call this user as {user.Information.ReferedAs}.
             """);

        if (user.Permissions.Count > 0)
        {
            builder.AppendLine("Here is the information about the permission of this user:");
            foreach (var (condition, respond) in user.Permissions)
            {
                builder.AppendLine($"- When {condition}, you should: {respond}");
            }
        }

        if (user.Notes.Count <= 0) 
            return;
        
        builder.AppendLine("Here are notes about this user:");
        foreach (var note in user.Notes)
        {
            builder.AppendLine($"- {note}");
        }
    }
}

public static class UserContextExtensions
{
    public static AgentSession AddUserContext(this AgentSession session, IServiceProvider provider)
    {
        session.AddContext(new UserContext(
            provider.GetRequiredService<ILogger<UserContext>>(),
            provider.GetRequiredService<UserManagementService>()
        ));
        return session;
    }
}
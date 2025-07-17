using System.ComponentModel;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using Calamus.Bot.Services.Music.Providers;
using Calamus.Database;
using Microsoft.Extensions.Options;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.API.Objects;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using Remora.Results;
using User = Calamus.Database.User;

namespace Calamus.Bot.Commands;

public enum MusicProvider
{
    Lastfm,
    Listenbrainz
}

[Group("config")]
public class ConfigCommand (IFeedbackService feedbackService, IInteractionCommandContext context, 
    CalamusDbContext dbContext, IOptions<LastFmConfig> lastFmOptions,
    IDiscordRestInteractionAPI interactionApi) : CommandGroup
{
    private readonly HttpClient _httpClient = new HttpClient();
    private readonly LastFmConfig _lastFmConfig = lastFmOptions.Value;
    
    [Command("link-lastfm")]
    [Description("Link your Last.fm account to Calamus.")]
    [Ephemeral]
    public async Task<Result> LinkLastFmAsync()
    { 
        if (!context.TryGetUserID(out var userId)) 
        {
            return Result.FromError(new InvalidOperationError("User ID not found in context."));
        } 

        // Check if the user already has a linked Last.fm account
        var dbUser = await dbContext.Users.FindAsync(userId.Value);
        if (dbUser is not null && !string.IsNullOrWhiteSpace(dbUser.LastFmUsername))
        {
            await feedbackService.SendContextualAsync($"Your Last.fm account is already linked as `{dbUser.LastFmUsername}`.");
            return Result.FromError(new InvalidOperationError("User already has a linked Last.fm account."));
        }

        string signature;
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(
                $"api_key{_lastFmConfig.ApiKey}methodauth.gettoken{_lastFmConfig.ApiSecret}");
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            signature = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        
        var apiCallUrl = $"{_lastFmConfig.BaseUrl}/?method=auth.gettoken&api_key={_lastFmConfig.ApiKey}&api_sig={signature}&format=json";
        var response = await _httpClient.GetAsync(apiCallUrl);
        
        if (!response.IsSuccessStatusCode)
        {
            await feedbackService.SendContextualAsync("Failed to retrieve Last.fm token. Please try again later.");
            return Result.FromError(new InvalidOperationError("Failed to retrieve Last.fm token."));
        }
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var tokenElement = doc.RootElement
            .GetProperty("token");
        var token = tokenElement.GetString();
        
        var userUrl = $"https://www.last.fm/api/auth/?api_key={_lastFmConfig.ApiKey}&token={token}";

        var embed = new EmbedBuilder()
            .WithAuthor("Log in to Last.fm")
            .WithDescription($"To link your Last.fm account, please log in using [this link]({userUrl}). \n" +
                             "You will be redirected to Last.fm to authorize Calamus to access your account. \n" +
                             "After authorizing, you just need to wait. \n" +
                             "(Note that we will not store your Last.fm session key, we only use it to retrieve your username.)")
            .WithColour(Color.Orange)
            .Build().Entity;
        
        await feedbackService.SendContextualEmbedAsync(embed);
        var applicationId = context.Interaction.ApplicationID;
        var interactionToken = context.Interaction.Token;
        
        //wait 1.5 minutes for the user to authorize the app
        Task.Delay(TimeSpan.FromMinutes(1)).Wait();
        
        // After the user has authorized the app, we need to get the session key
        using (var md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(
                $"api_key{_lastFmConfig.ApiKey}methodauth.getsessiontoken{token}{_lastFmConfig.ApiSecret}");
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            signature = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        var sessionKeyApiCallUrl = $"{_lastFmConfig.BaseUrl}/?method=auth.getsession&api_key={_lastFmConfig.ApiKey}&token={token}&api_sig={signature}&format=json";
        var sessionKeyResponse = await _httpClient.GetAsync(sessionKeyApiCallUrl);
        
        if (!sessionKeyResponse.IsSuccessStatusCode)
        {
            await interactionApi.EditOriginalInteractionResponseAsync(applicationId, interactionToken, 
                embeds: new([new Embed(Title: "Last.fm Linking Failed",
                    Description: "Failed to retrieve Last.fm session key. Please try again later.",
                    Colour: Color.Red)]),
                components: new ([]));
            return Result.FromError(new InvalidOperationError("Failed to retrieve Last.fm session key."));
        }
        
        var sessionKeyJson = await sessionKeyResponse.Content.ReadAsStringAsync();
        using var sessionKeyDoc = System.Text.Json.JsonDocument.Parse(sessionKeyJson);
        var sessionKeyElement = sessionKeyDoc.RootElement
            .GetProperty("session")
            .GetProperty("key");
        var sessionKey = sessionKeyElement.GetString();
        // Now we check the username of the user by calling user.getinfo while authenticated with the session key
        using (var md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(
                $"api_key{_lastFmConfig.ApiKey}methoduser.getinfosk{sessionKey}{_lastFmConfig.ApiSecret}");
            byte[] hashBytes = md5.ComputeHash(inputBytes);
            signature = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        var userInfoApiCallUrl = $"{_lastFmConfig.BaseUrl}/?method=user.getinfo&api_key={_lastFmConfig.ApiKey}&sk={sessionKey}&api_sig={signature}&format=json";
        var userInfoResponse = await _httpClient.GetAsync(userInfoApiCallUrl);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            await interactionApi.EditOriginalInteractionResponseAsync(applicationId, interactionToken, 
                embeds: new([new Embed(Title: "Last.fm Linking Failed",
                    Description: "Failed to retrieve Last.fm user info. Please try again later.",
                    Colour: Color.Red)]),
                components: new ([]));
            return Result.FromError(new InvalidOperationError("Failed to retrieve Last.fm user info."));
        }
        var userInfoJson = await userInfoResponse.Content.ReadAsStringAsync();
        using var userInfoDoc = System.Text.Json.JsonDocument.Parse(userInfoJson);
        var userInfoElement = userInfoDoc.RootElement
            .GetProperty("user");
        var lastFmUsername = userInfoElement.GetProperty("name").GetString();
        
        // Now we can link the Last.fm account to the user in the database
        if (dbUser is null)
        {
            dbUser = new User
            {
                Id = userId.Value,
                LastFmUsername = lastFmUsername,
                ChosenProvider = "LastFm"
            };
            await dbContext.Users.AddAsync(dbUser);
        }
        else
        {
            dbUser.LastFmUsername = lastFmUsername;
            dbContext.Users.Update(dbUser);
        }
        
        //edit the original interaction response to confirm the linking
        await dbContext.SaveChangesAsync();
        var confirmationEmbed = new EmbedBuilder()
            .WithAuthor("Last.fm Account Linked")
            .WithDescription($"Your Last.fm account has been successfully linked as `{lastFmUsername}`.")
            .WithColour(Color.Green)
            .Build().Entity;
        
        await interactionApi.EditOriginalInteractionResponseAsync(applicationId, interactionToken, 
            embeds: new([confirmationEmbed]),
            components: new ([]));
        
        
        return Result.Success;
    }
    
    [Command("link-listenbrainz")]
    [Description("Link your ListenBrainz account to Calamus.")]
    [Ephemeral]
    public async Task<Result> LinkListenBrainzAsync(
        [Description ("Your listenbrainz token from https://listenbrainz.org/settings/")] 
        string lbtoken)
    { 
        if (!context.TryGetUserID(out var userId)) 
        {
            return Result.FromError(new InvalidOperationError("User ID not found in context."));
        }
        
        var lbBaseUrl = "https://api.listenbrainz.org";
        var validateTokenUrl = $"{lbBaseUrl}/1/validate-token";
        
        var request = new HttpRequestMessage(HttpMethod.Get, validateTokenUrl);
        request.Headers.Add("Authorization", $"Token {lbtoken}");
        
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            await feedbackService.SendContextualAsync("Failed to validate ListenBrainz token. Please check your token and try again.");
            return Result.FromError(new InvalidOperationError("Failed to validate ListenBrainz token."));
        }
        
        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var userElement = doc.RootElement;
        var listenBrainzUsername = userElement.GetProperty("user_name").GetString();
        
        //Link the ListenBrainz account to the user in the database
        var dbUser = await dbContext.Users.FindAsync(userId.Value);
        if (dbUser is null)
        {
            dbUser = new User
            {
                Id = userId.Value,
                ListenBrainzUsername = listenBrainzUsername,
                ChosenProvider = "ListenBrainz"
            };
            await dbContext.Users.AddAsync(dbUser);
        }
        else
        {
            dbUser.ListenBrainzUsername = listenBrainzUsername;
            dbContext.Users.Update(dbUser);
        }
        
        await dbContext.SaveChangesAsync();
        
        var confirmationEmbed = new EmbedBuilder()
            .WithAuthor("ListenBrainz Account Linked")
            .WithDescription($"Your ListenBrainz account has been successfully linked as `{listenBrainzUsername}`.")
            .WithColour(Color.Green)
            .Build().Entity;
        
        await feedbackService.SendContextualEmbedAsync(confirmationEmbed);
        
        return Result.Success;
    }
    
    [Command("choose-provider")]
    [Description("Choose your preferred music provider for the bot to use.")]
    [Ephemeral]
    public async Task<Result> ChooseProviderAsync(
        [Description("The provider to choose.")] 
        MusicProvider provider)
    {
        if (!context.TryGetUserID(out var userId)) 
        {
            return Result.FromError(new InvalidOperationError("User ID not found in context."));
        }
        
        var dbUser = await dbContext.Users.FindAsync(userId.Value);
        if (dbUser is null)
        {
            await feedbackService.SendContextualAsync("You have not linked your account yet. \n" +
                                                      "You can link your account using `/config link-lastfm` or `/config link-listenbrainz`.");
            return Result.FromError(new InvalidOperationError("User has not linked their account."));
        }

        string providerName;
        switch (provider)
        {
            case MusicProvider.Lastfm:
                providerName = "LastFm";
                break;
            case MusicProvider.Listenbrainz:
                providerName = "ListenBrainz";
                break;
            default:
                await feedbackService.SendContextualAsync("Invalid music provider selected. Please choose either Last.fm or ListenBrainz.");
                return Result.FromError(new InvalidOperationError("Invalid music provider selected."));
        }
        
        dbUser.ChosenProvider = providerName;
        dbContext.Users.Update(dbUser);
        
        await dbContext.SaveChangesAsync();
        
        var confirmationEmbed = new EmbedBuilder()
            .WithAuthor("Music Provider Changed")
            .WithDescription($"Your preferred music provider has been changed to `{provider}`.")
            .WithColour(Color.Green)
            .Build().Entity;
        
        await feedbackService.SendContextualEmbedAsync(confirmationEmbed);
        
        return Result.Success;
    }

}
using System.ComponentModel;
using Calamus.Bot.Services;
using Microsoft.Extensions.Logging;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Discord.Extensions.Embeds;
using System.Drawing;
using Calamus.Database;
using Remora.Discord.Commands.Attributes;
using Remora.Discord.Commands.Feedback.Messages;
using Remora.Discord.Interactivity;
using Remora.Results;

namespace Calamus.Bot.Commands;

[Group("ai")]
public class AiCommand (ICommandContext context, IDiscordRestUserAPI userApi, 
    IFeedbackService feedbackService, MusicService musicService, GeminiService geminiService,
    CalamusDbContext dbContext) : CommandGroup
{
    [Command("roast")]
    [Description("Roast your Top 10 artists!")]
    public async Task<Result> RoastAsync(
        [Description("The user to roast. Defaults to the command invoker if not specified.")]
        IUser? user = null)
    {
        if (user is null)
        {
            if (!context.TryGetUserID(out var userId))
            {
                return Result.Success;
            }

            user = (await userApi.GetUserAsync(userId)).Entity;
        }
        
        //Check if the user has given permission to use the AI service
        var dbUser = await dbContext.Users.FindAsync(user.ID.Value);
        if (dbUser is null || !dbUser.AiPermissionGiven)
        {
            await feedbackService.SendContextualAsync("The selected User has not given permission to use the AI service. " +
                                                      "They need to allow to use AI Services on them by using `/ai allow.`");
            return Result.FromError(new InvalidOperationError("User has not linked their account or allowed AI service."));
        }
        
        var topArtists = await musicService.GetTopArtistsAsync(user.ID.ToString(), 10);
        var prompt = $"You are a music critic. Roast the top 10 artists of {user.Username} in a humorous way. " +
                     "Make it funny and engaging, but not too harsh. Only respond with your roast. " +
                     "Here are the top 10 artists: " +
                     string.Join(", ", topArtists.Select(a => a.Name));
        var response = await geminiService.ReplyToPromptAsync(prompt);

        // Discord Embed description limit: 4096 Zeichen
        const int embedLimit = 4096;
        if (response.Length > embedLimit)
        {
            for (int i = 0; i < response.Length; i += embedLimit)
            {
                var chunk = response.Substring(i, Math.Min(embedLimit, response.Length - i));
                var embed = new EmbedBuilder()
                    .WithAuthor($"Roast for {user.Username}")
                    .WithDescription(chunk)
                    .WithColour(Color.Orange)
                    .Build().Entity;
                await feedbackService.SendContextualEmbedAsync(embed);
            }
        }
        else
        {
            var embed = new EmbedBuilder()
                .WithAuthor($"Roast for {user.Username}")
                .WithDescription(response)
                .WithColour(Color.Orange)
                .Build().Entity;
            await feedbackService.SendContextualEmbedAsync(embed);
        }

        return Result.Success;
    }
    
    [Command("allow")]
    [Description("Allow AI permission for the user. This will allow the bot to use AI services on you.")]
    [Ephemeral]
    public async Task<Result> AllowAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.Success;
        }
        
        var dbUser = await dbContext.Users.FindAsync(userId.Value);
        if (dbUser is null)
        {
            await feedbackService.SendContextualAsync("You have not linked your account yet. \n" +
                                                      "You can link your account using `/link`.");
            return Result.FromError(new InvalidOperationError("User has not linked their account or allowed AI service."));
        }
        
        var embed = new EmbedBuilder()
            .WithAuthor("AI Permission")
            .WithDescription("Calamus uses the Google Gemini AI to provide you with features such as roasts. \n" +
                             "By allowing AI permission, you agree to [Google's Gemini \"Unpaid Services\" Terms](https://ai.google.dev/gemini-api/terms) \n" +
                             "(Calamus is operated and hosted in the EEA) \n" +
                             "You can revoke this permission at any time using `/ai revoke`.")
            .WithColour(Color.Orange)
            .Build().Entity;
        var option = new FeedbackMessageOptions(MessageComponents: new IMessageComponent[]
        {
            new ActionRowComponent(new[]
            {
                new ButtonComponent(
                    ButtonComponentStyle.Primary,
                    "Allow AI Permission",
                    CustomID: CustomIDHelpers.CreateButtonID("ai_allow")
                ),
                new ButtonComponent(
                    ButtonComponentStyle.Secondary,
                    "Cancel",
                    CustomID: CustomIDHelpers.CreateButtonID("ai_cancel")
                )
            })
        });
        
        await feedbackService.SendContextualEmbedAsync(embed, option);
        
        return Result.Success;
    }
    
    [Command("revoke")]
    [Description("Revoke AI permission for the user. This will prevent the bot from using AI services on you.")]
    [Ephemeral]
    public async Task<Result> RevokeAsync()
    {
            if (!context.TryGetUserID(out var userId))
            {
                return Result.Success;
            }
            
            var dbUser = await dbContext.Users.FindAsync(userId.Value);
            if (dbUser is null)
            {
                await feedbackService.SendContextualAsync("You have not linked your account yet. \n" +
                                                          "You can link your account using `/link-account`.");
                return Result.FromError(new InvalidOperationError("User has not linked their account or allowed AI service."));
            }
        
            dbUser.AiPermissionGiven = false;
            await dbContext.SaveChangesAsync();
        
            await feedbackService.SendContextualAsync($"AI permission has been revoked.");
        
            return Result.Success;
    }
}
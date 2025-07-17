using System.Drawing;
using Calamus.Database;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Extensions.Embeds;
using Remora.Discord.Interactivity;
using Remora.Rest.Core;
using Remora.Results;

namespace Calamus.Bot.Interactions;

public class AiAllowInteractions (CalamusDbContext dbContext, IInteractionCommandContext interactionCommandContext,
    IDiscordRestInteractionAPI interactionApi ) : InteractionGroup
{
    [Button("ai_allow")]
    public async Task<Result> AllowAsync()
    {
        if (!interactionCommandContext.TryGetUserID(out var userId))
        {
            return Result.FromError(new InvalidOperationError("User ID not found in context."));
        }

        var dbUser = await dbContext.Users.FindAsync(userId.Value);
        if (dbUser is null)
        {
            //This should not happen since we already check this in the AiCommand,
            //but we handle it gracefully here
        }
        else
        {
            dbUser.AiPermissionGiven = true;
            dbContext.Users.Update(dbUser);
        }
        
        await dbContext.SaveChangesAsync();
        
        var embed = new EmbedBuilder()
            .WithAuthor("AI Service Permission Granted")
            .WithDescription("You have successfully granted permission to use AI services on your account.")
            .WithColour(Color.Green)
            .Build().Entity;
        
        var applicationId = interactionCommandContext.Interaction.ApplicationID;
        var interactionToken = interactionCommandContext.Interaction.Token;
        
        await interactionApi.EditOriginalInteractionResponseAsync(applicationId, interactionToken, 
            embeds: new([embed]),
            components: new ([]));
        
        return Result.FromSuccess();
    }
    
    [Button("ai_cancel")]
    public async Task<Result> DenyAsync()
    {
        if (!interactionCommandContext.TryGetUserID(out var userId))
        {
            return Result.FromError(new InvalidOperationError("User ID not found in context."));
        }
        
        await dbContext.SaveChangesAsync();
        
        var embed = new EmbedBuilder()
            .WithAuthor("AI Service Permission Denied")
            .WithDescription("You have successfully denied permission to use AI services on your account.")
            .WithColour(Color.Red)
            .Build().Entity;
        
        var applicationId = interactionCommandContext.Interaction.ApplicationID;
        var interactionToken = interactionCommandContext.Interaction.Token;
        
        
        var result = await interactionApi.EditOriginalInteractionResponseAsync(applicationId, interactionToken, 
            embeds: new([embed]),
            components: new ([]));
        
        return Result.FromSuccess();
    }
}
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using Calamus.Bot.Services;
using Calamus.Bot.Services.Music.Models;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.API;
using Remora.Discord.API.Abstractions.Objects;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Commands.Contexts;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;
using Remora.Discord.Extensions.Embeds;

namespace Calamus.Bot.Commands;

public class NowPlayingCommand(ICommandContext context, IDiscordRestUserAPI userApi, IFeedbackService feedbackService, MusicService musicService) : CommandGroup
{
    [Command("np")]
    [Description("Returns the last or currently playing track")]
    public async Task<Result> NowPlayingAsync()
    {
        if (!context.TryGetUserID(out var userId))
        {
            return Result.Success;
        }

        var user = (await userApi.GetUserAsync(userId)).Entity;

        var (nowPlaying, isCurrent) = await musicService.GetNowPlayingAsync(userId.ToString());
        
        if (nowPlaying == null)
        {
            await feedbackService.SendContextualEmbedAsync(
                new EmbedBuilder()
                    .WithAuthor("Now Playing")
                    .WithDescription("Kein Track gefunden.")
                    .WithColour(Color.Red)
                    .Build().Entity);
            return Result.Success;
        }

        var currentString = isCurrent ? "current" : "last";
        
        var baseAvatarUrl = CDN.GetUserAvatarUrl(user);
        var avatarUrlString = baseAvatarUrl.IsDefined() ? baseAvatarUrl.Entity.ToString() : null;
        var albumCoverUrl = nowPlaying.AlbumCoverUrl;
        if (string.IsNullOrEmpty(albumCoverUrl))
        {
            albumCoverUrl = "https://cdn.discordapp.com/embed/avatars/0.png"; // Fallback to default avatar if no album cover is available
        }
        await feedbackService.SendContextualEmbedAsync(
            new EmbedBuilder()
                .WithAuthor($"{user.GlobalName}'s {currentString} song:", iconUrl:avatarUrlString)
                .WithTitle(nowPlaying.Track ?? "Unknown Track")
                .WithDescription($"**{nowPlaying.Artist}** - {nowPlaying.Album}")
                .WithColour(Color.LimeGreen)
                .WithThumbnailUrl(albumCoverUrl)
                .Build().Entity);
        return Result.Success;
    }
}
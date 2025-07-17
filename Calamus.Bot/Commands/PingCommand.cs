using System.ComponentModel;
using Remora.Commands.Attributes;
using Remora.Commands.Groups;
using Remora.Discord.Commands.Feedback.Services;
using Remora.Results;

namespace Calamus.Bot.Commands;

public class PingCommand(IFeedbackService feedbackService) : CommandGroup
{
    [Command("ping")]
    [Description("Check if the bot is responsive")]
    public async Task<Result> PingAsync()
    {
        await feedbackService.SendContextualAsync("Pop!");
        
        return Result.Success;
    }
}
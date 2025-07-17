namespace Calamus.Bot;

public sealed class CalamusConfig
{
    public required string BotToken { get; set; }
    public ulong[]? DebugServerId { get; set; }
}
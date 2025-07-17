namespace Calamus.Bot.Services.Music.Models;

public class NowPlayingInfo
{
    public NowPlayingResult? Result { get; set; }
    public bool IsPlaying { get; set; }

    public void Deconstruct(out NowPlayingResult? nowPlaying, out bool isCurrent)
    {
        nowPlaying = Result;
        isCurrent = IsPlaying;
    }
}
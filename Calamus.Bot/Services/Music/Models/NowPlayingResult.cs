namespace Calamus.Bot.Services.Music.Models;

public class NowPlayingResult {
    public string? Track { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? Provider { get; set; }
    
    public string? TrackUrl { get; set; }
    
    public string? AlbumCoverUrl { get; set; }
}
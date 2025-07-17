namespace Calamus.Bot.Services.Music.Providers;

public class LastFmConfig
{
    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://ws.audioscrobbler.com/2.0/";
    
    public string ApiSecret { get; set; } = string.Empty;
}


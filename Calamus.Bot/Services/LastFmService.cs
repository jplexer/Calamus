using Calamus.Bot.Services.Music.Models;
using Calamus.Bot.Services.Music.Providers;
using Calamus.Database;
using Microsoft.Extensions.Options;

namespace Calamus.Bot.Services;

public class LastFmService(IOptions<LastFmConfig> config, CalamusDbContext dbContext)
{
    private readonly LastFmConfig _config = config.Value;
    private readonly HttpClient _httpClient = new HttpClient();

    public async Task<NowPlayingInfo> GetNowPlayingAsync(string userId) {
        var apiKey = _config.ApiKey;
        var user = await dbContext.Users.FindAsync(ulong.Parse(userId));
        if (user is null || string.IsNullOrEmpty(user.LastFmUsername))
        {
            return new NowPlayingInfo
            {
                Result = null,
                IsPlaying = false
            };
        }
        var userName = user.LastFmUsername;
        var apiCallUrl = $"{_config.BaseUrl}/?method=user.getrecenttracks&format=json&limit=1&api_key={apiKey}&user={userName}";

        var response = await _httpClient.GetAsync(apiCallUrl);
        if (!response.IsSuccessStatusCode)
        {
            return new NowPlayingInfo
            {
                Result = null,
                IsPlaying = false
            };
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var trackElement = doc.RootElement
            .GetProperty("recenttracks")
            .GetProperty("track")[0];

        var track = trackElement.GetProperty("name").GetString();
        var artist = trackElement.GetProperty("artist").GetProperty("#text").GetString();
        var album = trackElement.GetProperty("album").GetProperty("#text").GetString();
        var isPlaying = trackElement.TryGetProperty("@attr", out var attr) && attr.TryGetProperty("nowplaying", out var nowPlaying) && nowPlaying.GetString() == "true";
        var trackUrl = trackElement.GetProperty("url").GetString();
        string albumCoverUrl = "";
        if (trackElement.TryGetProperty("image", out var images))
        {
            foreach (var img in images.EnumerateArray())
            {
                if (img.GetProperty("size").GetString() == "extralarge")
                {
                    albumCoverUrl = img.GetProperty("#text").GetString();
                    break;
                }
            }
        }
        
        return new NowPlayingInfo
        {
            Result = new NowPlayingResult
            {
                Track = track,
                Artist = artist,
                Album = album,
                Provider = "Last.fm",
                TrackUrl = trackUrl,
                AlbumCoverUrl = albumCoverUrl
            },
            IsPlaying = isPlaying
        };
    }
    
    public async Task<Artist[]> GetTopArtistsAsync(string userId, int limit = 10) {
        var apiKey = _config.ApiKey;
        var user = await dbContext.Users.FindAsync(ulong.Parse(userId));
        if (user is null || string.IsNullOrEmpty(user.LastFmUsername))
        {
            return [];
        }
        var userName = user.LastFmUsername;
        var apiCallUrl = $"{_config.BaseUrl}/?method=user.gettopartists&format=json&limit={limit}&api_key={apiKey}&user={userName}";

        var response = await _httpClient.GetAsync(apiCallUrl);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var artistsElement = doc.RootElement
            .GetProperty("topartists")
            .GetProperty("artist");

        return artistsElement.EnumerateArray().Select(a => new Artist
        {
            Name = a.GetProperty("name").GetString(),
            PlayCount = int.Parse(a.GetProperty("playcount").GetString() ?? string.Empty),
            ImageUrl = a.GetProperty("image").EnumerateArray()
                .FirstOrDefault(img => img.GetProperty("size").GetString() == "extralarge")
                .GetProperty("#text").GetString(),
            Url = a.GetProperty("url").GetString(),
            Mbid = a.GetProperty("mbid").GetString()
        }).ToArray();
    }
}

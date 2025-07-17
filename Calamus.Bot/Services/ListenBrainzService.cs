using Calamus.Bot.Services.Music.Models;
using Calamus.Database;

namespace Calamus.Bot.Services;

public class ListenBrainzService (CalamusDbContext dbContext)
{
    private readonly HttpClient _httpClient = new HttpClient();
    
    
    //TODO: For ListenBrainz, they dont have Album Cover URLs, so we need to the MusicBrainz API to get the album cover URLs
    public async Task<NowPlayingInfo> GetNowPlayingAsync(string userId)
    {
        var user = await dbContext.Users.FindAsync(ulong.Parse(userId));
        if (user is null || string.IsNullOrEmpty(user.ListenBrainzUsername))
        {
            return new NowPlayingInfo
            {
                Result = null,
                IsPlaying = false
            };
        }
        var userName = user.ListenBrainzUsername;
        var requestUrl = $"https://api.listenbrainz.org/1/user/{userName}/playing-now";
        
        var response = await _httpClient.GetAsync(requestUrl);
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
        if (!doc.RootElement.TryGetProperty("payload", out var payload) || 
            !payload.TryGetProperty("listens", out var listens) || 
            listens.GetArrayLength() == 0)
        {
            // we first check the last song played, if that doesn't exist, we return null
            
            var checkLastUrl = $"https://api.listenbrainz.org/1/user/{userName}/listens?count=1";
            var lastResponse = await _httpClient.GetAsync(checkLastUrl);
            if (!lastResponse.IsSuccessStatusCode)
            {
                return new NowPlayingInfo
                {
                    Result = null,
                    IsPlaying = false
                };
            }
            var lastJson = await lastResponse.Content.ReadAsStringAsync();
            using var lastDoc = System.Text.Json.JsonDocument.Parse(lastJson);
            if (!lastDoc.RootElement.TryGetProperty("payload", out var lastPayload) || 
                !lastPayload.TryGetProperty("listens", out var lastListens) || 
                lastListens.GetArrayLength() == 0)
            {
                return new NowPlayingInfo
                {
                    Result = null,
                    IsPlaying = false
                };
            }
            
            var lastTrackElement = lastListens[0].GetProperty("track_metadata");
            var lastTrack = lastTrackElement.GetProperty("track_name").GetString();
            var lastArtist = lastTrackElement.GetProperty("artist_name").GetString();
            var lastAlbum = lastTrackElement.GetProperty("release_name").GetString();
            
            return new NowPlayingInfo
            {
                Result = new NowPlayingResult
                {
                    Track = lastTrack,
                    Artist = lastArtist,
                    Album = lastAlbum,
                    Provider = "ListenBrainz"
                },
                IsPlaying = false // Last listen is not currently playing
            };
            
        }
        var trackElement = listens[0].GetProperty("track_metadata");
        var track = trackElement.GetProperty("track_name").GetString();
        var artist = trackElement.GetProperty("artist_name").GetString();
        var album = trackElement.GetProperty("release_name").GetString();
        
        return new NowPlayingInfo
        {
            Result = new NowPlayingResult
            {
                Track = track,
                Artist = artist,
                Album = album,
                Provider = "ListenBrainz"
            },
            IsPlaying = true
        };
    }
    public async Task<Artist[]> GetTopArtistsAsync(string userId, int limit = 10)
    {
        var user = await dbContext.Users.FindAsync(ulong.Parse(userId));
        if (user is null || string.IsNullOrEmpty(user.ListenBrainzUsername))
        {
            return [];
        }
        var userName = user.ListenBrainzUsername;
        var requestUrl = $"https://api.listenbrainz.org/1/stats/user/{userName}/artists?count={limit}";
        
        var response = await _httpClient.GetAsync(requestUrl);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }
        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("payload", out var payload) || 
            !payload.TryGetProperty("artists", out var topArtists) || 
            topArtists.GetArrayLength() == 0)
        {
            return [];
        }
        
        var artists = new List<Artist>();
        foreach (var artistElement in topArtists.EnumerateArray())
        {
            var name = artistElement.GetProperty("artist_name").GetString();
            var playCount = artistElement.GetProperty("listen_count").GetInt32();
            var mbid = artistElement.TryGetProperty("artist_mbid", out var mbId) ? mbId.GetString() : null;
            artists.Add(new Artist
            {
                Name = name,
                PlayCount = playCount,
                Mbid = mbid,
            });
        }
        return artists.ToArray();
    }
    
}
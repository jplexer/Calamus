using Calamus.Bot.Services.Music.Models;
using Calamus.Bot.Services.Music.Providers;
using Calamus.Bot.Services.Music;
using Calamus.Database;
using Microsoft.Extensions.Options;

namespace Calamus.Bot.Services;

public class MusicService (ListenBrainzService listenBrainzService, LastFmService lastFmService, CalamusDbContext dbContext)
{
    private async Task<string> GetPreferredMusicProviderAsync(string userId)
    {
        // get user from database
        var user = await dbContext.Users.FindAsync(ulong.Parse(userId));
        if (user is null)
        {
            return "";
        }
        //get user's preferred music provider
        return user.ChosenProvider switch
        {
            "ListenBrainz" => "ListenBrainz",
            "LastFm" => "LastFm",
            _ => ""
        };
        
    }
    public async Task<NowPlayingInfo> GetNowPlayingAsync(string userId)
    {
        //Get user's preferred music provider
        var provider = await GetPreferredMusicProviderAsync(userId);
        if (provider.Equals("ListenBrainz", StringComparison.OrdinalIgnoreCase))
        {
            return await listenBrainzService.GetNowPlayingAsync(userId);
        } 
        
        if (provider.Equals("LastFm", StringComparison.OrdinalIgnoreCase))
        {
            return await lastFmService.GetNowPlayingAsync(userId);
        }
            
        //return empty NowPlayingInfo if no provider is set
        return new NowPlayingInfo
        {
            Result = null,
            IsPlaying = false
        };
    }
    
    public async Task<Artist[]> GetTopArtistsAsync(string userId, int limit) {
        //Get user's preferred music provider
        var provider = await GetPreferredMusicProviderAsync(userId);
        if (provider.Equals("ListenBrainz", StringComparison.OrdinalIgnoreCase))
        {
            return await listenBrainzService.GetTopArtistsAsync(userId, limit);
        }

        if (provider.Equals("LastFm", StringComparison.OrdinalIgnoreCase))
        {
            return await lastFmService.GetTopArtistsAsync(userId, limit);
        }
        
        //return empty array if no provider is set
        return [];
    }
}

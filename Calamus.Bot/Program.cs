using Calamus.Bot;
using Calamus.Bot.Interactions;
using Calamus.Bot.Services;
using Calamus.Bot.Services.Music.Providers;
using Calamus.Database;
using Remora.Discord.API.Abstractions.Rest;
using Remora.Discord.Caching.Extensions;
using Remora.Discord.Commands.Extensions;
using Remora.Discord.Commands.Services;
using Remora.Discord.Extensions.Extensions;
using Remora.Discord.Hosting.Extensions;
using Remora.Discord.Interactivity.Extensions;
using Remora.Rest.Core;

var builder = Host.CreateApplicationBuilder(args);

var config = builder.Configuration.GetSection("Calamus").Get<CalamusConfig>()
    ?? throw new InvalidOperationException("Calamus configuration is missing or invalid.");

builder.Services
    .AddDiscordService(_ => config.BotToken)
    .AddDiscordCaching()
    .AddDiscordCommands(enableSlash: true)
    .AddInteractivity()
    .AddCommandGroupsFromAssembly(typeof(Program).Assembly)
    .AddScoped<MusicService>()
    .AddScoped<GeminiService>()
    .AddScoped<ListenBrainzService>()
    .AddScoped<LastFmService>()
    .AddInteractionGroup<AiAllowInteractions>()
    .Configure<CalamusConfig>(builder.Configuration.GetSection("Calamus"))
    .Configure<LastFmConfig>(builder.Configuration.GetSection("LastFm"))
    .Configure<GeminiConfig>(builder.Configuration.GetSection("Gemini"))
    .AddMemoryCache();

builder.AddNpgsqlDbContext<CalamusDbContext>(connectionName: "calamus-db");

var host = builder.Build();
using (var scope = host.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CalamusDbContext>();
    await dbContext.Initialize();
}

var slashService = host.Services.GetRequiredService<SlashService>();

if (config.DebugServerId?.Length > 0)
{
    foreach (var serverId in config.DebugServerId)
    {
        if (builder.Environment.IsDevelopment())
        {
            var guildId = new Snowflake(serverId);
            await slashService.UpdateSlashCommandsAsync(guildId);
        }
        else
        {
            var appInfo = await host.Services.GetRequiredService<IDiscordRestOAuth2API>()
                .GetCurrentBotApplicationInformationAsync();

            var api = host.Services.GetRequiredService<IDiscordRestApplicationAPI>();
            await api.BulkOverwriteGuildApplicationCommandsAsync(appInfo.Entity.ID, new Snowflake(serverId), []);
        }
    }
}
else
{
    await slashService.UpdateSlashCommandsAsync();
}

host.Run();

using Discord.Interactions;
using Discord.WebSocket;
using RootedBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddYamlFile("config.yml", false);
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = Discord.GatewayIntents.Guilds |
                            Discord.GatewayIntents.GuildMembers |
                            Discord.GatewayIntents.GuildMessages |
                            Discord.GatewayIntents.MessageContent
        }));
        services.AddSingleton<InteractionService>();
        services.AddHostedService<InteractionHandlingService>();
        services.AddHostedService<DiscordStartupService>();
        services.AddHostedService<DumpParserService>();
    })
    .Build();

await host.RunAsync();
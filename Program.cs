using Discord.Interactions;
using Discord.WebSocket;
using RootedBot.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config =>
    {
        config.AddYamlFile("config.yml", false);       // Add the config file to IConfiguration variables
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = Discord.GatewayIntents.Guilds | 
                            Discord.GatewayIntents.GuildMembers | 
                            Discord.GatewayIntents.GuildMessages | 
                            Discord.GatewayIntents.MessageContent
        }));       // Add the discord client to services with proper intents
        services.AddSingleton<InteractionService>();        // Add the interaction service to services
        services.AddHostedService<InteractionHandlingService>();    // Add the slash command handler
        services.AddHostedService<DiscordStartupService>();         // Add the discord startup service
    })
    .Build();
//set bot status

    

await host.RunAsync();
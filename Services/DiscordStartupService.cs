using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootedBot.Modules;
using System.Threading;
using System.Threading.Tasks;

namespace RootedBot.Services
{
    public class DiscordStartupService : IHostedService
    {
        private readonly DiscordSocketClient _discord;
        private readonly IConfiguration _config;
        private readonly ILogger<DiscordSocketClient> _logger;
        private readonly ILogger<Welcomer> _welcomerLogger;
        private Welcomer _welcomer;

        public DiscordStartupService(DiscordSocketClient discord, IConfiguration config, ILogger<DiscordSocketClient> logger, ILogger<Welcomer> welcomerLogger)
        {
            _discord = discord;
            _config = config;
            _logger = logger;
            _welcomerLogger = welcomerLogger;

            _discord.Log += msg => LogHelper.OnLogAsync(_logger, msg);

        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _welcomer = new Welcomer(_discord, _welcomerLogger);

            await _discord.LoginAsync(TokenType.Bot, _config["token"]);
            await _discord.StartAsync();
            await _discord.SetGameAsync(
                "RootedBot | /help",
                "https://www.twitch.tv/FlameGrowl",
                type: ActivityType.Streaming
            );
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _discord.LogoutAsync();
            
            await _discord.StopAsync();
        }

    }
}

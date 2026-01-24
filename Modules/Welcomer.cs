using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace RootedBot.Modules
{
    public class Welcomer
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<Welcomer> _logger;

        public Welcomer(DiscordSocketClient client, ILogger<Welcomer> logger = null)
        {
            _client = client;
            _logger = logger;
            _client.UserJoined += OnUserJoined;
        }

        private async Task OnUserJoined(SocketGuildUser user)
        {
            _logger?.LogInformation($"User {user.Username} joined guild {user.Guild.Name}");
            
            var channel = user.Guild.GetTextChannel(1464546742161899622UL);
            if (channel != null)
            {
                EmbedBuilder embed = new EmbedBuilder()
                    .WithTitle("Welcome to the Server!")
                    .WithDescription($"Hello {user.Mention}, welcome to **{user.Guild.Name}**! We're glad to have you here. Please make sure to check out the rules and introduce yourself.")
                    .WithColor(Color.Green)
                    .WithAuthor(user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                    .WithCurrentTimestamp();
                await channel.SendMessageAsync(embed: embed.Build());
                _logger?.LogInformation($"Welcome message sent to channel {channel.Name}");
            }
            else
            {
                _logger?.LogWarning($"Could not find channel with ID 1464546742161899622 in guild {user.Guild.Name}");
            }
        }
    }
}
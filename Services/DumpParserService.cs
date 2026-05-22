using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootedBot.Utility;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace RootedBot.Services
{
    /// <summary>
    /// Watches every message in a ticket-* channel. If a .txt attachment is
    /// detected and it is a valid Rooted dump, the bot replies with a parsed embed.
    /// </summary>
    public class DumpParserService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<DumpParserService> _logger;
        private readonly HttpClient _http = new();

        public DumpParserService(DiscordSocketClient client, ILogger<DumpParserService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= OnMessageReceivedAsync;
            return Task.CompletedTask;
        }

        private async Task OnMessageReceivedAsync(SocketMessage message)
        {
            // Ignore bots and non-guild messages
            if (message.Author.IsBot) return;
            if (message.Channel is not SocketTextChannel channel) return;

            // Only act inside ticket channels
            if (!channel.Name.StartsWith("ticket-")) return;

            // Look for .txt attachments
            foreach (var attachment in message.Attachments)
            {
                if (!attachment.Filename.EndsWith(".txt")) continue;

                string content;
                try
                {
                    content = await _http.GetStringAsync(attachment.Url);
                }
                catch
                {
                    _logger.LogWarning("Failed to download attachment {Filename}", attachment.Filename);
                    continue;
                }

                // Check if it is actually a Rooted dump
                if (!DumpParser.IsRootedDump(content))
                {
                    _logger.LogInformation("Attachment {Filename} is not a Rooted dump, skipping.", attachment.Filename);
                    continue;
                }

                var dump = DumpParser.Parse(content);
                _logger.LogInformation("Auto-parsed Rooted dump {TicketId} in channel {Channel}", dump.TicketId, channel.Name);

                var embed = new EmbedBuilder()
                    .WithTitle("Rooted Dump Detected & Parsed")
                    .WithDescription($"A Rooted dump file uploaded by {message.Author.Mention} was automatically parsed.")
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .AddField("Ticket ID",   dump.TicketId,              inline: false)
                    .AddField("Platform",    dump.Platform,              inline: true)
                    .AddField("OS Version",  dump.OsVersion,             inline: true)
                    .AddField("App Version", dump.AppVersion,            inline: true)
                    .AddField("Branch",      dump.Branch,                inline: true)
                    .AddField("Dev Mode",    dump.DevMode,               inline: true)
                    .AddField("First Run",   dump.FirstRun,              inline: true)
                    .AddField("Install Path",$"`{dump.InstallPath}`",    inline: false)
                    .WithFooter("Auto-parsed by RootedBot")
                    .Build();

                await channel.SendMessageAsync(embed: embed);

                // Only parse the first valid dump per message
                break;
            }
        }
    }
}
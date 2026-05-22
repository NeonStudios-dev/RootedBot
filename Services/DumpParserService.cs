using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootedBot.Utility;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
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
                var embedBuilder2 = new EmbedBuilder()
                    .WithTitle("Rooted Dump Detected & Parsed")
                    .WithDescription(string.Format("A Rooted dump file uploaded by {0} was automatically parsed.", message.Author.Mention))
                    .WithColor(Color.Blue)
                    .WithCurrentTimestamp()
                    .AddField("Ticket ID", dump.TicketId, inline: false)
                    .AddField("Platform", dump.Platform, inline: true)
                    .AddField("OS Version", dump.OsVersion, inline: true)
                    .AddField("App Version", dump.AppVersion, inline: true)
                    .AddField("Branch", dump.Branch, inline: true)
                    .AddField("Dev Mode", dump.DevMode, inline: true)
                    .AddField("First Run", dump.FirstRun, inline: true)
                    .AddField("Install Path", string.Format("`{0}`", dump.InstallPath), inline: false)
                    .WithFooter("Auto-parsed by RootedBot");

                AddCrashFields(embedBuilder2, dump);

                var embed = embedBuilder2.Build();
                await channel.SendMessageAsync(embed: embed);

            }
        }
        private static void AddCrashFields(EmbedBuilder embed, DumpInfo dump)
        {
            if (!dump.HasCrashes)
            {
                embed.AddField("Crash Logs", "✅ No errors or crashes found in dump.", inline: false);
                return;
            }

            var shown = dump.CrashLogs.Take(3).ToList();

            foreach (var entry in shown)
            {
                var summary = entry.Summary.Length > 900
                    ? entry.Summary.Substring(0, 900) + "…"
                    : entry.Summary;

                var title = string.Format("⚠️ [{0}] {1}", entry.Level, entry.Category);
                var body = string.Format("`{0}`\n{1}", entry.Timestamp, summary);

                if (entry.ExitCode >= 0)
                    body += string.Format("\nExit code: `{0}`", entry.ExitCode);

                if (!string.IsNullOrWhiteSpace(entry.Session))
                    body += string.Format("\nSession: `{0}`", entry.Session);

                embed.AddField(title, body, inline: false);
            }

            if (dump.CrashLogs.Count > 3)
            {
                embed.AddField(
                    "More entries",
                    string.Format("…and {0} more error(s) in the dump file.", dump.CrashLogs.Count - 3),
                    inline: false);
            }
        }


    }
}
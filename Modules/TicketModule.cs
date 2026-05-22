using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RootedBot.Utility;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
namespace RootedBot.Modules
{
    public class TicketModule : InteractionModuleBase<SocketInteractionContext>
    {
        private const ulong SupportRoleId = 1507310799973126154UL;

        // Tracks open tickets: userId -> channelId
        private static readonly ConcurrentDictionary<ulong, ulong> OpenTickets = new();

        // ─── /ticket command ───────────────────────────────────────────────────────

        [SlashCommand("ticket", "Open a support ticket. Optionally attach a Rooted dump file.")]
        public async Task CreateTicket(IAttachment dumpFile = null)
        {
            await DeferAsync(ephemeral: true);

            var user = (SocketGuildUser)Context.User;
            var guild = Context.Guild;

            // One ticket per user
            if (OpenTickets.TryGetValue(user.Id, out var existingChannelId))
            {
                var existing = guild.GetTextChannel(existingChannelId);
                if (existing != null)
                {
                    await FollowupAsync($"You already have an open ticket: {existing.Mention}", ephemeral: true);
                    return;
                }
                // Channel was deleted externally, clean up
                OpenTickets.TryRemove(user.Id, out _);
            }

            // Parse dump if attached
            DumpInfo dump = null;
            if (dumpFile != null)
            {
                try
                {
                    using var http = new HttpClient();
                    var content = await http.GetStringAsync(dumpFile.Url);
                    if (DumpParser.IsRootedDump(content))
                        dump = DumpParser.Parse(content);
                }
                catch { /* ignore parse failures, open ticket anyway */ }
            }

            // Create private channel
            var shortId = dump != null
                ? (dump.TicketId.Length >= 8 ? dump.TicketId[..8] : dump.TicketId)
                : user.Id.ToString()[..8];

            var channel = await guild.CreateTextChannelAsync($"ticket-{shortId}", props =>
            {
                props.PermissionOverwrites = new[]
                {
                    new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Deny)),
                    new Overwrite(user.Id, PermissionTarget.User,
                        new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)),
                    new Overwrite(SupportRoleId, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow, manageMessages: PermValue.Allow)),
                };
                // props.CategoryId = YOUR_CATEGORY_ID;
            });

            OpenTickets[user.Id] = channel.Id;

            // Build embed
            var embedBuilder = new EmbedBuilder()
                .WithTitle("Support Ticket Opened")
                .WithColor(Color.Orange)
                .WithAuthor(user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithCurrentTimestamp()
                .WithFooter("Opened by " + user.Username);

            if (dump != null)
            {
                embedBuilder
                    .WithDescription("A Rooted dump file was detected and parsed automatically.")
                    .AddField("Ticket ID", dump.TicketId, inline: false)
                    .AddField("Platform", dump.Platform, inline: true)
                    .AddField("OS Version", dump.OsVersion, inline: true)
                    .AddField("App Version", dump.AppVersion, inline: true)
                    .AddField("Branch", dump.Branch, inline: true)
                    .AddField("Dev Mode", dump.DevMode, inline: true)
                    .AddField("First Run", dump.FirstRun, inline: true)
                    .AddField("Install Path", $"`{dump.InstallPath}`", inline: false);
                    AddCrashFields(embedBuilder, dump);


            }
            else
            {
                embedBuilder.WithDescription(
                    dumpFile != null
                        ? "The attached file could not be recognised as a Rooted dump. Please describe your issue below."
                        : "No dump file provided. Please describe your issue below and a support member will assist you shortly.");
            }

            // Buttons
            var buttons = new ComponentBuilder()
                .WithButton("Close Ticket", "ticket_close", ButtonStyle.Danger, new Emoji("🔒"))
                .WithButton("Mark Resolved", "ticket_resolve", ButtonStyle.Success, new Emoji("✅"))
                .Build();

            await channel.SendMessageAsync(
                text: $"<@&{SupportRoleId}> — New ticket from {user.Mention}",
                embed: embedBuilder.Build(),
                components: buttons
            );

            await FollowupAsync($"Your ticket has been opened: {channel.Mention}", ephemeral: true);
        }

        // ─── Button interactions ───────────────────────────────────────────────────

        [ComponentInteraction("ticket_close")]
        public async Task CloseTicketButton()
        {
            var channel = Context.Channel as SocketTextChannel;
            if (channel == null || !channel.Name.StartsWith("ticket-"))
            {
                await RespondAsync("This can only be used in a ticket channel.", ephemeral: true);
                return;
            }

            // Remove from tracking
            foreach (var kv in OpenTickets)
            {
                if (kv.Value == channel.Id)
                {
                    OpenTickets.TryRemove(kv.Key, out _);
                    break;
                }
            }

            var confirmButtons = new ComponentBuilder()
                .WithButton("Yes, close it", "ticket_close_confirm", ButtonStyle.Danger, new Emoji("🔒"))
                .WithButton("Cancel", "ticket_close_cancel", ButtonStyle.Secondary, new Emoji("✖️"))
                .Build();

            await RespondAsync("Are you sure you want to close this ticket?", components: confirmButtons, ephemeral: false);
        }

        [ComponentInteraction("ticket_close_confirm")]
        public async Task CloseTicketConfirm()
        {
            await RespondAsync("Closing ticket in 5 seconds...");
            await Task.Delay(5000);
            await (Context.Channel as SocketTextChannel)!.DeleteAsync();
        }

        [ComponentInteraction("ticket_close_cancel")]
        public async Task CloseTicketCancel()
        {
            await RespondAsync("Close cancelled.", ephemeral: true);
        }

        [ComponentInteraction("ticket_resolve")]
        public async Task ResolveTicket()
        {
            var channel = Context.Channel as SocketTextChannel;
            if (channel == null || !channel.Name.StartsWith("ticket-"))
            {
                await RespondAsync("This can only be used in a ticket channel.", ephemeral: true);
                return;
            }

            // Remove from tracking
            foreach (var kv in OpenTickets)
            {
                if (kv.Value == channel.Id)
                {
                    OpenTickets.TryRemove(kv.Key, out _);
                    break;
                }
            }

            var embed = new EmbedBuilder()
                .WithTitle("Ticket Resolved")
                .WithDescription($"This ticket was marked as resolved by {Context.User.Mention}.")
                .WithColor(Color.Green)
                .WithCurrentTimestamp()
                .Build();

            var closeButton = new ComponentBuilder()
                .WithButton("Close Channel", "ticket_close_confirm", ButtonStyle.Danger, new Emoji("🔒"))
                .Build();

            await RespondAsync(embed: embed, components: closeButton);
        }

        private static void AddCrashFields(EmbedBuilder embed, DumpInfo dump)
        {
            if (!dump.HasCrashes)
            {
                embed.AddField("Crash Logs", " No errors or crashes found in dump.", inline: false);
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


        // ─── Auto dump parser — fires on any message with an attachment ────────────

        [ComponentInteraction("ticket_parse_dump")]
        public async Task ParseDumpButton() { /* placeholder, parsing is event-driven via MessageReceived */ }
    }
}
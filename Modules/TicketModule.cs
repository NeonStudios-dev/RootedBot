using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using RootedBot.Utility;
using System.Threading.Tasks;

namespace RootedBot.Modules
{
    public class TicketModule : InteractionModuleBase<SocketInteractionContext>
    {
        // Replace with your actual support role ID
        private const ulong SupportRoleId = 1464377870213316638UL;

        [SlashCommand("ticket", "Open a support ticket by attaching a Rooted dump file.")]
        public async Task CreateTicket(IAttachment dumpFile)
        {
            await DeferAsync(ephemeral: true);

            // Download and parse the dump file
            DumpInfo dump;
            try
            {
                using var http = new System.Net.Http.HttpClient();
                var content = await http.GetStringAsync(dumpFile.Url);
                dump = DumpParser.Parse(content);
            }
            catch
            {
                await FollowupAsync("Failed to read the dump file. Make sure you attached a valid Rooted dump (.txt).", ephemeral: true);
                return;
            }

            var guild = Context.Guild;
            var user = Context.User as SocketGuildUser;

            // Create a private channel named ticket-<short ticket id>
            var shortId = dump.TicketId.Length >= 8 ? dump.TicketId[..8] : dump.TicketId;
            var channelName = $"ticket-{shortId}";

            var channel = await guild.CreateTextChannelAsync(channelName, props =>
            {
                props.PermissionOverwrites = new[]
                {
                    // Deny everyone by default
                    new Overwrite(guild.EveryoneRole.Id, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Deny)),

                    // Allow the ticket creator
                    new Overwrite(user.Id, PermissionTarget.User,
                        new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow)),

                    // Allow the support role
                    new Overwrite(SupportRoleId, PermissionTarget.Role,
                        new OverwritePermissions(viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, readMessageHistory: PermValue.Allow, manageMessages: PermValue.Allow)),
                };

                // Optionally put tickets in a category — set a category ID here if you have one
                // props.CategoryId = YOUR_CATEGORY_ID;
            });

            // Build the embed
            var embed = new EmbedBuilder()
                .WithTitle($"Support Ticket — {dump.TicketId}")
                .WithColor(Color.Orange)
                .WithAuthor(user.Username, user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl())
                .WithCurrentTimestamp()
                .AddField("Ticket ID", dump.TicketId, inline: false)
                .AddField("Platform", dump.Platform, inline: true)
                .AddField("OS Version", dump.OsVersion, inline: true)
                .AddField("App Version", dump.AppVersion, inline: true)
                .AddField("Branch", dump.Branch, inline: true)
                .AddField("Dev Mode", dump.DevMode, inline: true)
                .AddField("First Run", dump.FirstRun, inline: true)
                .AddField("Install Path", $"`{dump.InstallPath}`", inline: false)
                .WithFooter("Opened by " + user.Username)
                .Build();

            await channel.SendMessageAsync(
                text: $"<@&{SupportRoleId}> — New ticket from {user.Mention}",
                embed: embed
            );

            await FollowupAsync($"Your ticket has been opened: {channel.Mention}", ephemeral: true);
        }

        [SlashCommand("closeticket", "Close and delete this support ticket channel.")]
        [RequireUserPermission(GuildPermission.ManageChannels)]
        public async Task CloseTicket()
        {
            if (Context.Channel.Name.StartsWith("ticket-"))
            {
                await RespondAsync("Closing ticket in 5 seconds...", ephemeral: false);
                await Task.Delay(5000);
                await (Context.Channel as SocketTextChannel)!.DeleteAsync();
            }
            else
            {
                await RespondAsync("This command can only be used inside a ticket channel.", ephemeral: true);
            }
        }
    }
}

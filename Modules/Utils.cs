using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace RootedBot.Modules
{
    public class Utils : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ping", "Check the bot's latency.")]
        public async Task Ping()
        {
            var latency = Context.Client.Latency;
            await RespondAsync($"Pong! The bot's latency is {latency} ms.", ephemeral: true);
        }
        [SlashCommand("userinfo", "Get information about a user.")]
        public async Task UserInfo(SocketGuildUser user)
        {
            await RespondAsync($"User Info for {user.Mention}:\nJoined Server: {user.JoinedAt}\nCreated Account: {user.CreatedAt}", ephemeral: true);
        }
        [SlashCommand("serverinfo", "Get information about the server.")]
        public async Task ServerInfo()
        {
            var guild = Context.Guild;
            await RespondAsync($"Server Info for {guild.Name}:\nMember Count: {guild.MemberCount}\nCreated On: {guild.CreatedAt}", ephemeral: true);
        }
        [SlashCommand("avatar", "Get the avatar URL of a user.")]
        public async Task Avatar(SocketGuildUser user)
        {
            await RespondAsync($"{user.Mention}'s avatar: {user.GetAvatarUrl()}", ephemeral: true);
        }
        [SlashCommand("update", "Send embed to update channel that a new rooted version is available.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task Update()
        {
            var guild = Context.Guild;
            var updateChannel = guild.GetTextChannel(1464358094350843924);
            var embed = new EmbedBuilder()
                .WithTitle("New Rooted Version Available!")
                .WithDescription("A new version of Rooted Bot has been released. Please update to the latest version for new features and improvements.")
                .WithColor(Color.Blue)
                .WithCurrentTimestamp()
                .WithAuthor(Context.Client.CurrentUser)
                .Build();

            await updateChannel.SendMessageAsync(embed: embed);
            await RespondAsync("Update notification sent!", ephemeral: true);
            await updateChannel.SendMessageAsync("||<@&1464377870213316638>||");
        }
        [SlashCommand("clearupdates", "Clear all messages in the update channel.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task ClearUpdates()
        {
            var guild = Context.Guild;
            var updateChannel = guild.GetTextChannel(1464358094350843924);
            await updateChannel.DeleteMessagesAsync(await updateChannel.GetMessagesAsync(100).FlattenAsync());
            await RespondAsync("Update channel cleared!", ephemeral: true);
        }
    
    
    
    
    
    
    
    
    }
}

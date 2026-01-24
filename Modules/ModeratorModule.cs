using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Threading.Tasks;

namespace RootedBot.Modules
{
    public class ModeratorModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("kick", "Kick the specified user.")]
        [RequireUserPermission(GuildPermission.KickMembers)]
        [RequireBotPermission(GuildPermission.KickMembers)]
        public async Task Kick(SocketGuildUser user)
        {
            await ReplyAsync($"cya {user.Mention} :wave:");
            await user.KickAsync();
        }
        [SlashCommand("ban", "Ban the specified user.")]
        [RequireUserPermission(GuildPermission.BanMembers)]
        [RequireBotPermission(GuildPermission.BanMembers)]
        public async Task Ban(SocketGuildUser user)
        {
            await ReplyAsync($"banned {user.Mention} :wave:");
            await user.BanAsync();
        }

    }
}

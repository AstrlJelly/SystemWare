using System.Text.Json.Serialization;
using CaretakerCoreNET;
using Discord;
using Discord.WebSocket;

namespace SystemWare.Persistence
{
    public class GuildPersist(ulong guildId)
    {
        public ulong GuildId = guildId;
        public string GuildName = "";
        public string Prefix = MainHook.DEFAULT_PREFIX;

        public void Init(DiscordSocketClient client, ulong guildId)
        {
            GuildId = guildId;
            SocketGuild? guild = client.GetGuild(guildId);
            if (guild == null) {
                // LogError($"guild was null!! am i still in the guild with id \"{guildId}\"?");
                CaretakerCore.LogError($"guild data with id \"{guildId}\" was null.");
                return;
            }
            GuildName = guild.Name;
        }
    }
}
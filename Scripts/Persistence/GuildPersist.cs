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

        public class CachedWareWebhook
        {
            public readonly IWebhook Webhook;
            public readonly DateTimeOffset LastAccessed;
            public string LastAvatarUrl = "";
            internal CachedWareWebhook(IWebhook Webhook, DateTimeOffset LastAccessed)
            {
                this.Webhook = Webhook;
                this.LastAccessed = LastAccessed;
            }
        }
        [JsonIgnore] public readonly Dictionary<ulong, CachedWareWebhook> WareWebhooks = [];

        public void AddCachedWebhook(IIntegrationChannel channel, IWebhook webhook)
        {
            WareWebhooks.Add(channel.Id, new(webhook, DateTimeOffset.Now));
        }

        public CachedWareWebhook? GetCachedWareWebhook(IIntegrationChannel channel)
        {
            if (WareWebhooks.TryGetValue(channel.Id, out var cachedWebhook)) {
                // make new webhook if too long since accessed? might be pointless
                // if ((DateTimeOffset.Now - cachedWebhook.LastAccessed).TotalHours > 1) {
                //
                // }
                return cachedWebhook;
            } else {
                return null;
            }
        }

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
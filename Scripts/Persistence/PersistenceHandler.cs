using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using CaretakerCoreDiscordNET;
using Discord;

namespace SystemWare.Persistence
{
    public class PersistenceHandler
    {
        public Dictionary<ulong, GuildPersist> GuildData { get; private set; } = [];
        public Dictionary<ulong, UserPersist> UserData { get; private set; } = [];

        // data can very much so be null, but i trust any user (basically just me) to never use data if it's null.
        public bool TryGetGuildData(ulong id, out GuildPersist data) 
        {
            data = GetGuildData(id)!;
            return data != null;
        }
        public bool TryGetGuildData(IUserMessage msg, out GuildPersist data) 
        {
            data = GetGuildData(msg)!;
            return data != null;
        }
        // returns null if not in guild, like if you're in dms
        public GuildPersist? GetGuildData(IUserMessage msg) => GetGuildData(msg.GetGuild()?.Id ?? 0);
        public GuildPersist GetGuildData(IGuild guild) => GetGuildData(guild.Id)!;
        public GuildPersist? GetGuildData(ulong id)
        {
            if (id == 0) return null; // return null here, don't wanna try creating a GuildPersist for a null guild

            if (!GuildData.TryGetValue(id, out GuildPersist? value)) {
                value = new GuildPersist(id);
                GuildData.Add(id, value);
            }
            return value;
        }

        public bool TryGetUserData(ulong id, out UserPersist data) 
        {
            data = GetUserData(id);
            return data != null;
        }
        public UserPersist GetUserData(IUserMessage msg) => GetUserData(msg.Author.Id);
        public UserPersist GetUserData(IUser user) => GetUserData(user.Id);
        public UserPersist GetUserData(ulong id)
        {
            if (!UserData.TryGetValue(id, out UserPersist? value)) {
                value = new UserPersist();
                value.Init(CaretakerCoreDiscordNET.Discord.Client, id);
                UserData.Add(id, value);
            }
            return value;
        }

        public async Task Save()
        {
            await Task.WhenAll(
                Voorhees.SaveGuilds(GuildData),
                Voorhees.SaveUsers(UserData)
            );
        }

        public async Task Load()
        {
            var loadGuilds = Task.Run(async () => {
                GuildData = await Voorhees.LoadGuilds();
                // not necessary, takes 5-10 ms to complete at 15 guilds. yeeeouch
                // also yes, i need to do that GetGuild().GetUser(), so that i can get the bot as IGuildUser :(
                GuildData = GuildData.OrderBy(x => CaretakerCoreDiscordNET.Discord.Client.GetGuild(x.Key)?.GetUser(MainHook.SYSTEMWARE_ID)?.JoinedAt?.UtcTicks).ToDictionary();
                //LogDebug("GuildData.Count : " + GuildData.Count);
                foreach (var key in GuildData.Keys) {
                    if (key > 0) {
                        if (GuildData[key] == null) {
                            GuildData[key] = new(key);
                        }
                    } else {
                        GuildData.Remove(key);
                    }
                    GuildData[key].Init(CaretakerCoreDiscordNET.Discord.Client, key);
                }

            });
            var loadUsers = Task.Run(async () => {
                UserData = await Voorhees.LoadUsers();
                // also not necessary, and also takes 5-10 ms to complete at 76 users
                UserData = UserData.OrderBy(x => x.Value.Username).ToDictionary();
                //LogDebug("UserData.Count : " + UserData.Count);
                foreach (var key in UserData.Keys) {
                    if (key > 0) {
                        if (UserData[key] == null) {
                            UserData[key] = new();
                        }
                    } else {
                        UserData.Remove(key);
                    }
                    UserData[key].Init(CaretakerCoreDiscordNET.Discord.Client, key);
                }
            });
            await Task.WhenAll(loadGuilds, loadUsers);
        }
    }
}


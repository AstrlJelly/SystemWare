using System;
using System.Text.Json.Serialization;

using Discord;
using Discord.WebSocket;

using CaretakerCoreNET;
using SystemWare.Wares;

namespace SystemWare.Persistence
{
    public class UserPersist
    {
        // is most likely not used, that's okay!
        [JsonInclude] bool IsInServer = true;
        public ulong UserId;
        [JsonIgnore] private IUser? user;
        [JsonIgnore] public IUser User {
            get {
                user ??= CaretakerCoreDiscordNET.Discord.Client.GetUser(UserId);
                return user;
            }
        }
        public string Username = "";

        public readonly Dictionary<string, Ware> AllWares = [];
        public string CurrentAutoProxy = "";
        public Ware? CurrentAutoProxyWare => AllWares.TryGetValue(CurrentAutoProxy, out Ware? val) ? val : null;

        // misc
        public long Timeout = 0;

        public void Init(DiscordSocketClient client, ulong userId)
        {
            UserId = userId;
            user = client.GetUser(userId);
            if (user != null) {
                Username = user.Username;
                IsInServer = true;
            } else {
                IsInServer = false;
            }
            Update();
        }

        [JsonInclude] bool NeedsUpdating;
        private void Update()
        {
            if (!NeedsUpdating) return;
            NeedsUpdating = true;
        }

        public bool TryAddWare(string name, string bracketsText, out Ware? ware)
        {
            ware = null;
            int textIndex = bracketsText.IndexOf("text");
            if (textIndex < 0) return false;

            ware = new Ware {
                name = name,
            };
            ware.AddBrackets(bracketsText[..textIndex], bracketsText[(textIndex + 4)..]);
            return AllWares.TryAdd(name, ware);
        }

        public bool TryGetWare(string name, out Ware? ware)
        {
            return AllWares.TryGetValue(name, out ware);
        }
    }
}
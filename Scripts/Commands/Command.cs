using System;
using CaretakerCoreDiscordNET;
using CaretakerCoreNET;
using Discord;
using Discord.WebSocket;

namespace SystemWare.Commands
{
    public class Command
    {
        /// <summary>
        /// yeah
        /// </summary>
        /// <param name="params">parsed params in a dictionary.</param>
        /// <param name="unparams">unparsed params; the strings that get parsed to the params dictionary.</param>
        /// <param name="infParams">the "params" input, which is always an array.</param>
        public class ParsedParams(string command, Dictionary<string, dynamic?> @params, Dictionary<string, string?> unparams, string[]? infParams)
        {
            public dynamic? this[string key] {
                get => Params[key];
            }
            // add a bool to use Unparams, had it as one "this[]" but that didn't really work
            public string? this[string key, bool doesntMatter] {
                get => Unparams[key];
            }

            public readonly string Command = command;
            public readonly Dictionary<string, dynamic?> Params = @params;
            public readonly Dictionary<string, string?> Unparams = unparams;
            public readonly string[] InfParams = infParams ?? [];
        }

        public readonly string Name;
        public readonly string Desc;
        // public readonly string Genre;
        public readonly HashSet<string> Genres;
        public delegate Task RunAsync(IUserMessage msg, ParsedParams p);
        public readonly RunAsync Func;
        public readonly Param[] Params;
        public readonly Param? Inf = null;
        public HashSet<ChannelPermission> LimitedToPerms;
        public HashSet<ulong> LimitedToIds;
        public int Timeout;

        public Command(string name, string desc, string genre, RunAsync? func = null, List<Param>? parameters = null, HashSet<ChannelPermission>? limitedToPerms = null, HashSet<ulong>? limitedToIds = null, int timeout = 500)
        {
            Name = name;
            Desc = desc;
            Genres = genre.Split(", ").ToHashSet();
            Func = func ?? ((_, _) => Task.CompletedTask);
            LimitedToPerms = limitedToPerms ?? [];
            LimitedToIds = limitedToIds ?? [];
            Timeout = timeout;
            // CurrentTimeout = 0;

            if (parameters != null) {
                if (parameters.TryFindIndex((x, _) => x.Name == "params", out int infIndex)) {
                    Inf = parameters[infIndex];
                    parameters.RemoveAt(infIndex);
                }
                Params = [..parameters];
            } else {
                Params = [];
            }
        }

        public bool IsGenre(string genre)
        {
            return Genres.Contains(genre);
        }

        public bool HasPerms(IUserMessage msg) {
            var guild = msg.GetGuild();
            if (guild == null) return true;
            // this is silly. but afaik it's the only way
            IGuildUser user = guild.GetUser(msg.Author.Id);
            if (msg.Channel is IGuildChannel channel) {
                return HasPerms(user, channel);
            } else {
                CaretakerCore.LogError("user or channel was not the right type! what the fuck");
                return false;
            }
        }
        public bool HasPerms(IGuildUser user, IGuildChannel chnl)
        {
            if (LimitedToPerms.Count <= 0 && LimitedToIds.Count <= 0) return true; // most common case
            if (LimitedToIds.Contains(user.Id)) return true; // id is easiest to check first
            var userPerms = user.GetPermissions(chnl);
            foreach (var perm in LimitedToPerms) {
                if (userPerms.Has(perm)) return true;
            }
            return false;
        }
    }


    public class Param(string name, string desc, dynamic preset, Param.ParamType? type = null, bool required = false)
    {
        public enum ParamType
        {
            String,
            Boolean,
            Double,
            Decimal,
            Integer,
            // UInteger,
            Long,
            User,
            Channel,
            Guild,
        }
        public dynamic? ToType(string str, SocketGuild? guild)
        {
            return Type switch {
                ParamType.Boolean     => str == "true",
                ParamType.Double      => double.TryParse(str, out double @double)    ? @double  : Preset,
                ParamType.Decimal     => decimal.TryParse(str, out decimal @decimal) ? @decimal : Preset,
                ParamType.Integer     => int.TryParse(str, out int @int)             ? @int     : Preset,
                ParamType.Long        => long.TryParse(str, out long @long)          ? @long    : Preset,
                ParamType.User        => MainHook.Instance.Client.ParseUser(str, guild),
                ParamType.Channel     => guild?.ParseChannel(str),
                ParamType.Guild       => MainHook.Instance.Client.ParseGuild(str),
                ParamType.String or _ => str, // gotta always have that "or _" :)
            };
        }

        public string Name = name;
        public string Desc = desc;
        public dynamic Preset = preset;
        public bool Required = required;
        
        public ParamType Type = type ?? preset.GetType().Name switch
        {
            "Boolean" => ParamType.Boolean,
            "Double" => ParamType.Double,
            "Int32" => ParamType.Integer,
            "Int64" => ParamType.Long,
            "String" or _ => ParamType.String,
        };
    }
}
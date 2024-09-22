using System;
using System.Diagnostics;
using System.Linq;
using System.Data;
using System.Text;

using Discord;
using Discord.WebSocket;
using CaretakerCoreDiscordNET;
using CaretakerCoreNET;
using SystemWare.Persistence;
using static CaretakerCoreDiscordNET.Discord;
using SystemWare.Wares;

namespace SystemWare.Commands
{
    public static class CommandHandler
    {
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public static readonly Command[] commands = [
            new("prefix", "Set the prefix for the current guild.", "commands", async (msg, p) => {
                if (!MainHook.Instance.Persist.TryGetGuildData(msg, out GuildPersist s) || s == null) return;
                s.Prefix = string.IsNullOrEmpty(p["prefix"]) ? ">" : p["prefix"]!;
                _ = msg.React("✅");
            }, [
                new Param("prefix", $"The prefix to set to. If empty, resets to \"{MainHook.DEFAULT_PREFIX}\".", "")
            ], [ ChannelPermission.ManageChannels ]),

            new("help, listCommands", "List all commands.", "commands", async (msg, p) => {
                await HelpReply(msg, p["command"] ?? "", p["listParams"], false);
            }, [ 
                new Param("command", "The command to get help for. (if empty, just lists all)", ""),
                new Param("listParams", "List parameters?", true)
            ]),

            new("import", "Import wares from tupperbox/pluralkit.", "wares", async (msg, p) => {
                switch (p["from"])
                {
                    case "tupperbox" or "tupper" or "tb": {

                    } break;
                    case "pluralkit" or "pk": {

                    } break;
                    default: {

                    } break;
                }
            }, [
                new Param("from", "Specify if you're importing tupperbox/pluralkit.", "")
            ]),

            new("reg, register", "Register a new ware.", "wares", async (msg, p) => {
                if (!MainHook.Instance.Persist.TryGetUserData(msg.Author.Id, out UserPersist u) || u == null) return;
                bool success = u.TryAddWare(p["name"], p["brackets"], out Ware ware);
                if (success) {
                    // EXAMPLE
                    // Added `Alex (He/They)` (`alex`), use al:text to proxy.
                    string reply = $"Added `{ware.DisplayName}`";
                    if (ware.DisplayName != ware.name) {
                        reply += $" (`{ware.name}`),";
                    }
                    reply += $" use `{p["brackets"]}` to proxy.";
                    await msg.Reply(reply);
                } else {
                    // await msg.Reply("A ware of that name already exists! Use a different command to modify existing wares.");
                    await msg.Reply("Make sure to put \"text\" in between your brackets!");
                }
            }, [
                new Param("name", "The name of the new ware. This will be used to refer to the ware in commands.", "", required: true),
                new Param("brackets", "The brackets used to proxy with this ware.", "", required: true),
                new Param("displayName", "The display name of the new ware. This will be used when proxying with the ware.", ""),
            ]),

            new("name, rename", "Change the name of a ware.", "wares"),

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
            new("cmd", "Run more internal commands.", "hidden"),
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

            new("help", "list all cmd commands", "commands", async (msg, p) => {
                await HelpReply(msg, p["command"] ?? "", p["listParams"], true);
            }, [
                new Param("command", "the command to get help for (if empty, just lists all)", ""),
                new Param("listParams", "list parameters?", false)
            ]),

            new("echo", "huh", "silly", async (msg, p) => {
                string? reply = p["reply"];
                if (string.IsNullOrEmpty(reply)) return;

                if (!reply.Contains("@everyone") && !reply.Contains("@here") && reply != "") {
                    await Task.Delay(int.Abs(p["wait"]));
                    await msg.Reply((string?)p["reply"] ?? "", false);
                } else {
                    string[] replies = [ "stop that!!!", "hey you can't do that :(", "explode", "why...", ":(" ];
                    await msg.Reply(p["reply"] != "" ? replies.GetRandom()! : ":(");
                }
            }, [
                new Param("reply", "the message to echo", ":3"),
                new Param("wait", "how long to wait until replying", 0),
            ]),

            new("save", "save _s and _u", "internal", async (_, _) => await MainHook.Instance.Persist.Save()),
            new("load", "save _s and _u", "internal", async (_, _) => await MainHook.Instance.Persist.Load()),
        ];
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        private static readonly Dictionary<string, Command> Commands = [];
        private static readonly Dictionary<string, Command> CmdCommands = [];

        // public static CommandHandler instance = new();

        public static (Command?, string) ParseCommand(string command, string parameters, ulong userId)
        {
            var whichComms = Commands;
            if (command == "cmd" && MainHook.TrustedUsers.Contains(userId))
            {
                whichComms = CmdCommands;
                (command, parameters) = parameters.SplitByFirstChar(' ');
            }

            return (whichComms.TryGetValue(command.ToLower(), out var com) ? com : null, parameters);
        }
        // might wanna make it return what the command.func returns, though i don't know how helpful that would be
        public async static Task<bool> DoCommand(IUserMessage msg, Command com, string strParams, string commandName)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var guild = msg.GetGuild();
            Dictionary<string, dynamic?> paramDict = []; // main dictionary of parsed parameters
            Dictionary<string, string?> unparamDict = []; // dictionary of unparsed parameters; just the strings
            string[]? unparams = null;
            if (com.Params != null && (com.Params.Length > 0 || com.Inf != null))
            {
                bool isBetweenQuotes = false;   // 
                bool backslash = false;         // true if last character was '\'
                int currentParamIndex = 0;      // 
                Param? currentParam = null;     // null unless manually set using "paramName : value"
                List<string>? infParams = null; // inf params; gets converted to an array when setting unparams
                int stopState = 0;              // a way for the Action<int> to return in the method; 0 = nothing, 1 = break, 2 = return

                // a list of chars that get concatenated into either a parameter name or a parameter value
                List<char> currentStringAsChars = new(strParams.Length); // setting the capacity gives negligible change in performance but i think it's funny

                // dictionary of actions that have the current index as an input, accessed using different types of chars
                Dictionary<char, Action<int>> charActions = new() {
                    { '"', i => isBetweenQuotes = !isBetweenQuotes },
                    { '\\', i => backslash = true }, // used to be a check in the for loop; this is much more intuitive
                    { ' ', i => {
                        // just act like a normal character if between quotes or if the space won't signify a new param
                        if (isBetweenQuotes || (currentParamIndex >= (com.Params.Length - 1)) && (i < strParams.Length && i >= 0))
                        {
                            currentStringAsChars.Add(strParams[i]);
                            return;
                        }

                        // makes sure cases like ">echo thing  1000" don't break anything
                        // maybe just check if last character was a space?
                        if (currentStringAsChars.Count < 1) return;
                        // lets you do things like "paramName : value" or "paramName: value" or even "paramName :value"
                        if ((strParams.IsIndexValid(i + 1) && strParams[i + 1] == ':') || (strParams.IsIndexValid(i - 1) && strParams[i - 1] == ':')) return;

                        if (currentParamIndex >= com.Params.Length)
                        {
                            if (com.Inf != null)
                            {
                                infParams ??= [];
                            }
                            else
                            {
                                stopState = 1;
                                return;
                            }
                        }

                        var paramStr = string.Concat(currentStringAsChars);
                        if (infParams == null) {
                            // if it's not being manually set (and not adding to inf params), use currentParamIndex then add 1 to it
                            if (currentParam == null) {
                                currentParam = com.Params[currentParamIndex];
                                currentParamIndex++;
                            }
                            unparamDict.TryAdd(currentParam.Name, paramStr); // use TryAdd cuz i don't really care about duplicate keys
                            dynamic? paramVal = currentParam.ToType(paramStr, guild);
                            paramDict.TryAdd(currentParam.Name, paramVal);
                        } else {
                            infParams.Add(paramStr);
                        }
                        currentParam = null;
                        currentStringAsChars.Clear();
                    }},
                    { ';', i => {
                        string paramName = string.Concat(currentStringAsChars);
                        currentStringAsChars.Clear();
                        if (paramName == "params")
                        {
                            infParams = [];
                        }
                        else
                        {
                            Param? param = Array.Find(com.Params, p => p.Name == paramName);
                            if (param != null)
                            {
                                currentParam = param;
                            }
                            else
                            {
                                GuildPersist? s = MainHook.Instance.Persist.GetGuildData(msg);
                                _ = msg.Reply(
                                    $"Incorrect parameter name! Use \"{s?.Prefix ?? MainHook.DEFAULT_PREFIX}help {com.Name}\" to get params for {com.Name}.\n"+
                                    "If you meant to input plain text, do \"\\;\" instead of just \";\"");
                                stopState = 2;
                            }
                        }
                    }},
                };
                for (int i = 0; i < strParams.Length; i++)
                {
                    // if you can get the action from the character, and there's not a \ before the character
                    if (charActions.TryGetValue(strParams[i], out var action) && !backslash)
                    {
                        action.Invoke(i);
                    }
                    else
                    {
                        currentStringAsChars.Add(strParams[i]);
                        backslash = false;
                    }
                    // refer to stopLoop comment
                    if (stopState == 1)
                    {
                        break;
                    }
                    else if (stopState == 2)
                    {
                        return false;
                    }
                }
                // there might be a better way to do this? works super well rn tho
                if (currentStringAsChars.Count > 0)
                {
                    // make sure the action doesn't just add a space
                    isBetweenQuotes = false;

                    // arbitrary. no other reason i chose it 😊😊😊
                    charActions[' '].Invoke(-69);
                }

                // check if every param is actually in paramDict
                // much better than the old method! this practically guarantees safety :)
                foreach (var param in com.Params)
                {
                    if (!paramDict.ContainsKey(param.Name))
                    {
                        if (param.Required) {
                            await HelpReply(msg, com.Name, true);
                            return false;
                        }
                        // convert if it's needed; if the preset is a string but the param type is not supposed to be a string
                        // this is for users, guilds, etc.
                        dynamic? paramVal = 
                            param.Preset is string preset && param.Type != Param.ParamType.String ? 
                                param.ToType(preset, guild) : 
                                param.Preset;

                        paramDict.Add(param.Name, paramVal);
                    }
                }
                if (com.Inf != null)
                {
                    // set unparams to infParams as string[]
                    unparams = infParams?.ToArray() ?? [];
                    // add an array to paramDict that is infParams converted to "params"'s type
                    paramDict.Add("params", infParams?.Select(p => (object?)com.Inf.ToType(p, guild)).ToArray() ?? []);
                }
            }

            stopwatch.Stop();
            MainHook.Instance.LogDebug($"took {stopwatch.Elapsed.TotalMilliseconds} ms to parse parameters", true);
            
            await com.Func.Invoke(msg, new Command.ParsedParams(commandName, paramDict, unparamDict, unparams));
            return true;
        }

        private static async Task HelpReply(IUserMessage msg, string command, bool listParams, bool cmd = false, bool showHidden = false)
        {
            (string, string)[]? commandLists = ListCommands(msg, command, listParams, cmd, showHidden);
            if (commandLists == null)
            {
                _ = msg.Reply(
                    $"{command} isn't a command." + (string.IsNullOrEmpty(command) ? "\nor something went wrong..." : "")
                );
                return;
            }
            if (commandLists.Length <= 0)
            {
                _ = msg.Reply("There were no commands to list.");
                return;
            }

            var dictIndex = 0;
            Dictionary<string, Embed> commandEmbeds = commandLists.Select((genreAndCom, index) =>
            {
                (string genre, string com) = genreAndCom;
                return (new EmbedBuilder {
                    Title = "commands : " + genre,
                    Description = com
                }).WithCurrentTimestamp().Build();
            }).ToDictionary(x => commandLists[dictIndex++].Item1);

            // there should only be components if there's more than one genre
            // for example, when you select a command manually there won't be any components
            var components = commandEmbeds.Keys.Count > 1 ? new ComponentBuilder {
                ActionRows = [
                    new ActionRowBuilder().WithSelectMenu(
                        "Commands",
                        commandLists.Select((genreAndCom) => new SelectMenuOptionBuilder(genreAndCom.Item1, genreAndCom.Item1)).ToList(),
                        "Command Select"
                    )
                ]
            } : null;

            var helpMsg = await msg.ReplyAsync(components: components?.Build(), embed: commandEmbeds.Values.ElementAt(0));

            async Task<bool> OnDropdownChange(SocketMessageComponent args)
            {
                IUser reactUser = args.User;
                IMessage message = args.Message;
                ulong ruId = reactUser.Id;
                if (message.Id != helpMsg.Id || ruId == MainHook.SYSTEMWARE_ID)
                {
                    return false;
                }

                // if it's not the victim nor the caretaker, ephemerally tell them they can't use it
                if (ruId != msg.Author.Id)
                {
                    _ = args.RespondAsync("Not for you...", ephemeral: true);
                    return false;
                }

                if (args.Data.CustomId is "commands")
                {
                    var genre = string.Join("", args.Data.Values);
                    await helpMsg.ModifyAsync(m => m.Embed = commandEmbeds[genre]);
                }

                return false;
            }
            
            using var rs = new ComponentSubscribe(OnDropdownChange, helpMsg);
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                while (stopwatch.Elapsed.TotalSeconds < 60 && !rs.Destroyed)
                {
                    // await Task.Delay(100);
                }
                stopwatch.Stop();
            rs.Dispose();
        }

        private static (string, string)[]? ListCommands(IUserMessage msg, string? singleCom, bool listParams, bool cmd = false, bool showHidden = false)
        {
            var sw = new Stopwatch();
            sw.Start();
            var commandDict = cmd ? CmdCommands : Commands;
            if (!string.IsNullOrEmpty(singleCom) && !commandDict.ContainsKey(singleCom))
            {
                return null;
            }
            var s = MainHook.Instance.Persist.GetGuildData(msg);
            string prefix = s?.Prefix ?? MainHook.DEFAULT_PREFIX;

            Dictionary<string, List<Command>> comsSortedByGenre = [];
            if (string.IsNullOrEmpty(singleCom))
            {
                foreach (Command command in commandDict.Values)
                {
                    foreach (var genre in command.Genres)
                    {
                        if (!comsSortedByGenre.TryGetValue(genre, out List<Command>? commands))
                        {
                            commands = ([]);
                            comsSortedByGenre.Add(genre, commands);
                        }

                        commands.Add(command);
                    }
                }
            }
            else
            {
                var command = commandDict[singleCom];
                foreach (var genre in command.Genres)
                {
                    comsSortedByGenre.Add(genre, [ command ]);
                }
            }

            StringBuilder comListBuilder = new();
            List<(string, string)> listedCommands = [];
            foreach ((string key, List<Command> coms) in comsSortedByGenre)
            {
                comListBuilder.Clear();
                for (int i = 0; i < coms.Count; i++)
                {
                    var com = coms[i];
                    if ((coms.IsIndexValid(i - 1) && coms[i - 1].Name == com.Name) || 
                        (com.IsGenre("hidden") && !showHidden))
                    {
                        continue;
                    }

                    comListBuilder.Append(prefix);
                    comListBuilder.Append(com.Name);
                    if (com.Params != null && !listParams) {
                        IEnumerable<string> paramNames = com.Params.Select(x => x.Name);
                        comListBuilder.Append(" (");
                        comListBuilder.Append(string.Join(", ", paramNames));
                        if (com.Inf != null) comListBuilder.Append(", params");
                        comListBuilder.Append(')');
                    }

                    comListBuilder.Append(" : ");
                    comListBuilder.AppendLine(com.Desc);

                    if (com.Params != null && listParams) {
                        foreach (var param in com.Params) {
                            comListBuilder.Append("　-"); // that's an indentation, apparently
                            comListBuilder.Append(param.Name);
                            comListBuilder.Append(" : ");
                            comListBuilder.AppendLine(param.Desc);
                        }
                    }
                }
                listedCommands.Add((key, comListBuilder.ToString()));
            }
            sw.Stop();
            MainHook.Instance.LogDebug($"ListCommands() took {sw.ElapsedMilliseconds} milliseconds to complete");
            return listedCommands.Count > 0 ? [.. listedCommands] : null;
        }

        // currently an instance isn't needed; try avoiding one?
        // i.e just put the logic that would need an instance in a different script
        public static void Init()
        {
            var whichComms = Commands;
            foreach (var command in commands) {
                var commandNames = command.Name.Split(", ");
                foreach (var commandName in commandNames) {
                    whichComms.Add(commandName.ToLower(), command);
                }

                if (command.Name == "cmd") {
                    whichComms = CmdCommands;
                }
            }
        }
    }
}

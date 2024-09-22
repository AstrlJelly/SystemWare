using System;

using Discord;
using Discord.WebSocket;

using CaretakerCoreNET;

using SystemWare.Persistence;
using CaretakerCoreDiscordNET;
using SystemWare.Commands;
using System.Diagnostics;
using SystemWare.Wares;
using Discord.Webhook;

namespace SystemWare
{
    public class MainHook
    {
        public static readonly MainHook Instance = new();

        public const ulong SYSTEMWARE_ID = 1286879575288053862;
        public const string DEFAULT_PREFIX = "s!";

        public static readonly HashSet<ulong> TrustedUsers = [
            SYSTEMWARE_ID,      // should be obvious
            438296397452935169, // @astrljelly
            752589264398712834, // @antoutta
        ];
        public static readonly HashSet<ulong> BannedUsers = [];

        public readonly DiscordSocketClient Client;
        public readonly PersistenceHandler Persist;

        public YamlConfig Config { get; private set; } = new();

        public bool TestingMode { get; private set; }

        private bool keepRunning = true;

        private static void Main(string[] args) => Instance.MainAsync(args).GetAwaiter().GetResult();

        private MainHook()
        {
            Client = new DiscordSocketClient(
                new DiscordSocketConfig {
                    GatewayIntents = GatewayIntents.All,
                    LogLevel = LogSeverity.Info,
                    MessageCacheSize = 50,
                }
            );

            Client.Log += ClientLog;
            Client.MessageReceived += OnMessageReceieved;
            Client.Ready += OnClientReady;

            Persist = new PersistenceHandler();

            Config = ConfigHandler.Load().GetAwaiter().GetResult();

            // OnLog += log => {
            //     LogBuilder.AppendLine(log);
            // };

            Console.CancelKeyPress += delegate { Client.StopAsync(); };
            // AppDomain.CurrentDomain.UnhandledException += async delegate { await OnStop(); };

            // StartTime = CaretakerCore.DateNow();
        }

        private async Task MainAsync(string[] args)
        {
            TestingMode = args.Contains("testing") || args.Contains("-t");

            CaretakerCoreDiscordNET.Discord.Init(Client, new());
            CommandHandler.Init();

            await Client.LoginAsync(TokenType.Bot, Config.Token);
            await Client.StartAsync();

            while (keepRunning) await Task.Delay(100);
        }

        private async Task OnClientReady()
        {
            await Persist.Load();
            SaveLoop();
        }

        private static Task ClientLog(LogMessage message)
        {
            CaretakerCore.InternalLog($"{DateTime.Now,-19} [{message.Severity,8}] {message.Source}: {message.Message} {message.Exception}", false, (CaretakerCore.LogSeverity)message.Severity);
            return Task.CompletedTask;
        }

        public void LogDebug(object? m = null, bool t = false)
        {
            if (Config.DebugMode) {
                CaretakerCore.LogInfo(m, t);
            }
        }

        private async void SaveLoop()
        {
            _ = Persist.Save();
            await Task.Delay(30000);
        }

        private Task OnMessageReceieved(SocketMessage message)
        {
            if (message is IUserMessage msg)
            {
                MessageHandler(msg);
            }
            return Task.CompletedTask;
        }

        public void MessageHandler(IUserMessage msg)
        {
            var u = Persist.GetUserData(msg);
            var s = Persist.GetGuildData(msg);
            string prefix = s?.Prefix ?? DEFAULT_PREFIX;

            if (msg.Content.StartsWith(prefix))
            {
                HandleCommand(msg, u, prefix);
            }
            else if (u.AllWares.Count > 0)
            {
                HandleWare(msg, u);
            }
            else
            {
                // if (s != null) {
                //     ulong cId = msg.Channel.Id;

                //     Func<(string, string)?>[] funcs = [
                //         // () => HandleCountAndChain(msg),
                //     ];
                //     foreach (var func in funcs)
                //     {
                //         // epic tuples 😄😄😄
                //         (string emojiToParse, string reply) = func.Invoke() ?? ("", "");
                //         if (!string.IsNullOrEmpty(emojiToParse)) {
                //             await msg.React(emojiToParse);
                //         }
                //         if (!string.IsNullOrEmpty(reply)) {
                //             await msg.Reply(reply);
                //         }
                //     }
                // }
            }
        }

        private async void HandleCommand(IUserMessage msg, UserPersist u, string prefix)
        {
            // bool banned = BannedUsers.Contains(msg.Author.Id); // check if user is banned
            bool testing = TestingMode && !TrustedUsers.Contains(msg.Author.Id); // check if testing, and if user is valid
            // LogDebug(msg.Author.Username + " banned? : " + banned);
            LogDebug("testing mode on? : " + TestingMode);

            (string command, string parameters) = msg.Content[prefix.Length..].SplitByFirstChar(' ');
            if (string.IsNullOrEmpty(command) || testing) return;
            (Command? com, parameters) = CommandHandler.ParseCommand(command, parameters, msg.Author.Id);

            if (com == null) return;

            // HasPerms returns true if GetGuild is null! make sure there's no security concerns there
            if (!com.HasPerms(msg) && !TrustedUsers.Contains(msg.Author.Id))
            {
                await msg.Reply("You don't have the perms to do this!");
                return;
            }

            if (u.Timeout > CaretakerCore.DateNow())
            {
                LogDebug("timeout : " + u.Timeout);
                LogDebug("DateNow() : " + CaretakerCore.DateNow());
                u.Timeout += 1500;
                _ = msg.React("🕒");
                return;
            }

            // var typing = msg.Channel.EnterTypingState(
            //     new RequestOptions {
            //         Timeout = 1000,
            //     }
            // );
                try
                {
                    Stopwatch sw = new();
                    sw.Start();
                    await CommandHandler.DoCommand(msg, com, parameters, command);
                    u.Timeout = CaretakerCore.DateNow() + com.Timeout;
                    sw.Stop();
                    LogDebug($"parsing {prefix}{command} command took {sw.Elapsed.TotalMilliseconds} ms");
                }
                catch (Exception error)
                {
                    await msg.Reply(error.Message, false);
                    CaretakerCore.LogError(error);
                }
            // // might be not enough? or it might just not work.
            // await Task.Delay(1100);
            // typing.Dispose();
        }

        private async void HandleWare(IUserMessage msg, UserPersist u)
        {
            // yes, it's cool to send multiple messages at once.
            // but does it really have any real use?? i never see people use it
            Ware? replyWare = u.CurrentAutoProxyWare;
            string reply = "";
            foreach (Ware ware in u.AllWares.Values)
            {
                foreach (var brackets in ware.AllBrackets)
                {
                    var (bracketStart, bracketEnd) = brackets;
                    if (msg.Content.StartsWith(bracketStart) && msg.Content.EndsWith(bracketEnd))
                    {
                        replyWare = ware;
                        reply = msg.Content[bracketStart.Length..(msg.Content.Length - bracketEnd.Length)];
                    }
                }
            }
            if (replyWare != null && msg.Channel is IIntegrationChannel channel) {
                IReadOnlyCollection<IWebhook> allWebhooks = await channel.GetWebhooksAsync();
                IWebhook? webhook = allWebhooks.FirstOrDefault(w => w.Creator.Id == SYSTEMWARE_ID && w.Name == nameof(SystemWare));
                webhook ??= await channel.CreateWebhookAsync(replyWare.name);
                var webhookClient = new DiscordWebhookClient(webhook);

                AllowedMentionTypes pings = AllowedMentionTypes.None;
                if (msg.MentionedRoleIds.Count > 0) pings |= AllowedMentionTypes.Roles;
                if (msg.MentionedUserIds.Count > 0) pings |= AllowedMentionTypes.Users;
                if (msg.MentionedEveryone)          pings |= AllowedMentionTypes.Everyone;
                await webhookClient.SendMessageAsync(
                    reply,
                    msg.IsTTS,
                    (IEnumerable<Embed>)msg.Embeds,
                    replyWare.DisplayName,
                    null, // avatar, replace later
                    RequestOptions.Default,
                    new AllowedMentions(pings),
                    null, // msg.Components,
                    msg.Flags ?? MessageFlags.None,
                    msg.Thread?.Id,
                    msg.Thread?.Name,
                    msg.Tags.Select(t => t.Key).ToArray(),
                    (msg.Poll is Poll poll ?
                    new PollProperties()
                    {
                        Question = new PollMediaProperties {
                            Text = poll.Question.Text
                        },
                        Answers = (List<PollMediaProperties>)poll.Answers,
                        Duration = (uint)(poll.ExpiresAt - DateTimeOffset.Now).TotalHours,
                        AllowMultiselect = poll.AllowMultiselect,
                        LayoutType = poll.LayoutType,
                    } : null)
                );
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using Discord.Rest;
using Newtonsoft.Json;

namespace DiscordMessagePostBot
{
    class Program
    {
        bool ready = false;
        string dateFormat = "dddd MMMM d, h:m tt";
        int numMessagesToGrab = 10;
        DiscordSocketClient botAPI = new DiscordSocketClient();
        Emoji confirmEmoji = new Emoji("✅");
        Emoji maybeEmoji = new Emoji("⚠");
        Emoji cancelEmoji = new Emoji("❌");
        Emoji goodToGo = new Emoji("🎆");

        List<SocketUser> usersToNotify = new List<SocketUser>();
        SocketGuild guild;
        SocketTextChannel confirmationChannel;

        Settings settings;
        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task<Task> MainAsync()
        {
            try
            {
                try
                {
                    if (File.Exists("settings.json"))
                    {
                        using (var settingsFile = File.OpenText("settings.json"))
                        {
                            var json = settingsFile.ReadToEnd();
                            settings = JsonConvert.DeserializeObject<Settings>(json);
                        }
                    }
                    else
                    {
                        settings = new Settings();
                        using (StreamWriter sw = new StreamWriter("settings.json"))
                        {
                            sw.WriteLine(JsonConvert.SerializeObject(settings, Formatting.Indented));
                            Console.WriteLine("New settings.json file created. Please close this and enter all your information first");
                            Console.ReadKey();
                        }
                    }
                }
                catch (FormatException e)
                {
                    PostConsoleLine("Error parsing guild or channel IDs. Please make sure they're on their own lines with no other characters");
                    return Task.CompletedTask;
                }
                catch (Exception e)
                {
                    PostConsoleLine("Error reading key file! Please make sure you have a file named key.txt in the same directory as the exe");

                    return Task.CompletedTask;
                }
                await botAPI.LoginAsync(TokenType.Bot, settings.key);
                await botAPI.StartAsync();
                botAPI.Ready += ClientReady;
                botAPI.Log += LogMessage;
                PostConsoleLine("Login Status is " + botAPI.LoginState);
                await botAPI.SetStatusAsync(UserStatus.Online);
                botAPI.ReactionAdded += ReactionAdded;
                botAPI.ReactionRemoved += ReactionRemoved;
                var timeToCheck = TimeSpan.FromMinutes(5);
                var lastCheckedTime = DateTime.Now;
                var pressedC = false;

                while (true)
                {
                    if (Console.KeyAvailable)
                    {
                        var cKey = Console.ReadKey().Key;
                        if (cKey == ConsoleKey.C)
                        {
                            pressedC = true;
                        }
                    }
                    if ((DateTime.Now) - lastCheckedTime > timeToCheck || pressedC)
                    {
                        pressedC = false;
                        lastCheckedTime = DateTime.Now;
                        if (ready)
                        {
                            var messages = await confirmationChannel.GetMessagesAsync(numMessagesToGrab).FlattenAsync();
                            PostConsoleLine("Got last " + numMessagesToGrab + " messages");
                            var messageList = messages.ToList();
                            var messageDates = new List<DateTime>(messageList.Count);
                            var index = messageList.Count - 1;
                            PostConsoleLine("Checking message for new reactions");
                            var hadToAddReactions = false;
                            foreach (var listMessage in messageList)
                            {
                                var isSuccessful = DateTime.TryParseExact(listMessage.Content.Split('\n')[0], dateFormat, null, System.Globalization.DateTimeStyles.None, out DateTime date);
                                if (isSuccessful)
                                {
                                    messageDates.Add(date);
                                    var names = listMessage.Content.Split('\n');
                                    var messageToConfirm = (await confirmationChannel.GetMessageAsync(listMessage.Id) as RestUserMessage);

                                    if (messageToConfirm != null && messageToConfirm.Reactions.ContainsKey(confirmEmoji))
                                    {
                                        var messageContent = messageToConfirm.Content;
                                        var userConfirmations = await messageToConfirm.GetReactionUsersAsync(confirmEmoji, 20).FlattenAsync();
                                        var reactionsList = userConfirmations.ToList();
                                        var namesToAdd = new List<string>();
                                        messageContent = messageContent.Replace("\n\n\n", "\n");
                                        foreach (var user in reactionsList)
                                        {
                                            var socketUser = guild.GetUser(user.Id);
                                            if (!names.Contains("**" + NicknameOrFull(socketUser) + "**") && (user.Id != botAPI.CurrentUser.Id))
                                            {
                                                hadToAddReactions = true;
                                                if (names.Contains(NicknameOrFull(socketUser)))
                                                {
                                                    messageContent = messageContent.Replace("\n" + NicknameOrFull(socketUser), "");
                                                }
                                                PostConsoleLine("Found confirmation that wasn't edited in by " + NicknameOrFull(socketUser));
                                                messageContent += "\n**" + NicknameOrFull(socketUser) + "**";
                                            }
                                        }
                                        await messageToConfirm.ModifyAsync(x => x.Content = messageContent);
                                        var userWarnings = await messageToConfirm.GetReactionUsersAsync(maybeEmoji, 20).FlattenAsync();
                                        foreach (var user in userWarnings)
                                        {
                                            var socketUser = guild.GetUser(user.Id);
                                            if (names.Contains(NicknameOrFull(socketUser)) && (user.Id != botAPI.CurrentUser.Id))
                                            {
                                                hadToAddReactions = true;
                                                PostConsoleLine("Found warning that wasn't edited out by " + NicknameOrFull(socketUser));
                                                messageContent.Replace("\n**" + NicknameOrFull(socketUser) + "**", "");
                                            }
                                        }
                                        var userCancels = await messageToConfirm.GetReactionUsersAsync(cancelEmoji, 20).FlattenAsync();
                                        foreach (var user in userCancels)
                                        {
                                            var socketUser = guild.GetUser(user.Id);
                                            if (names.Contains(NicknameOrFull(socketUser)) && (user.Id != botAPI.CurrentUser.Id))
                                            {
                                                hadToAddReactions = true;
                                                PostConsoleLine("Found X that wasn't edited out by " + NicknameOrFull(socketUser));
                                                messageContent.Replace("\n**" + NicknameOrFull(socketUser) + "**", "");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    messageDates.Add(DateTime.MinValue + TimeSpan.FromDays(50));
                                }
                                index--;
                            }
                            if (!hadToAddReactions)
                            {
                                PostConsoleLine("Already caught up on reactions");
                            }

                            PostConsoleLine("Checking whether to post new date");
                            if ((messageDates.Max() - settings.numberOfDaysAhead) < DateTime.Now.Date)
                            {
                                Console.Write("Most recent date is " + messageDates.Max().ToString(dateFormat));
                                DateTime startDate = DateTime.MinValue;
                                if (messageDates.Max().Date < DateTime.Now.Date)
                                {
                                    startDate = DateTime.Now.Date;
                                }
                                else
                                {
                                    startDate = messageDates.Max();
                                }
                                startDate = startDate.Date;
                                startDate = startDate.AddHours(18.5f);
                                while ((startDate.Date < (DateTime.Now + settings.numberOfDaysAhead).Date))
                                {
                                    startDate = startDate.AddDays(1);
                                    var newDate = startDate.ToString(dateFormat);
                                    var newMessage = await confirmationChannel.SendMessageAsync(newDate + "\nPeople Confirmed:\n");
                                    await newMessage.AddReactionsAsync(new[] { confirmEmoji, maybeEmoji, cancelEmoji });
                                }
                            }

                            else
                            {
                                PostConsoleLine("Already caught up on messages, waiting " + timeToCheck.ToString("%m") + " minutes before checking again");
                            }
                        }
                    }
                    System.Threading.Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                PostConsoleLine("Error occurred: " + e.Message + "\nStack Trace:\n" + e.StackTrace, true);
                return Task.CompletedTask;
            }
        }

        private string NicknameOrFull(SocketGuildUser user)
        {
            return !string.IsNullOrEmpty(user.Nickname) ? user.Nickname : user.Username.Split('#')[0];
        }

        private async Task<Task> ReactionRemoved(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {
            var message = await arg1.GetOrDownloadAsync();
            if (message.Author.Id != botAPI.CurrentUser.Id)
            { return Task.CompletedTask; }
            var enoughPeople = message.Reactions[confirmEmoji].ReactionCount < 9;
            var oldContent = message.Content;
            if (arg3.Emote.Name == confirmEmoji.Name)
            {
                await message.ModifyAsync((x) => x.Content = oldContent.Replace("\n**" + arg3.UserId + "**", "")
                .Replace("\n" + arg3.User.ToString(), "")
                .Replace("\n" + ((IGuildUser)arg3.User.Value).Nickname, "")
                .Replace("\n**" + ((IGuildUser)arg3.User.Value).Nickname + "**", ""));
            }

            if (enoughPeople && message.Reactions.ContainsKey(goodToGo))
            {
                await message.RemoveReactionAsync(goodToGo, botAPI.CurrentUser);
            }
            return Task.CompletedTask;
        }

        private async Task<Task> ReactionAdded(Cacheable<IUserMessage, ulong> arg1, ISocketMessageChannel arg2, SocketReaction arg3)
        {

            var message = await arg1.GetOrDownloadAsync();
            if (message.Author.Id != botAPI.CurrentUser.Id)
            { return Task.CompletedTask; }
            var oldContent = message.Content;

            if (arg3.Emote.Name != confirmEmoji.Name && arg3.Emote.Name != maybeEmoji.Name && arg3.Emote.Name != cancelEmoji.Name)
            {
                PostConsoleLine("The emoji wasn't valid");
                if (arg3.User.IsSpecified)
                {
                    await message.RemoveReactionAsync(arg3.Emote, arg3.User.Value);
                }

                return Task.CompletedTask;
            }
            if (arg3.User.IsSpecified && !oldContent.Contains(((IGuildUser)arg3.User.Value).Nickname) && arg3.Emote.Name == confirmEmoji.Name)
            {
                await message.ModifyAsync((x) => x.Content = oldContent + "\n**" + ((IGuildUser)arg3.User.Value).Nickname + "**");
            }
            var enoughPeople = message.Reactions[confirmEmoji].ReactionCount > 9;
            if (enoughPeople)
            {
                await message.AddReactionAsync(goodToGo);
            }
            return Task.CompletedTask;
        }

        private async Task<Task> ClientReady()
        {
            ready = true;
            PostConsoleLine("Client is ready");
            guild = botAPI.GetGuild(settings.guildId);
            PostConsoleLine("Got guild with name " + guild.Name);
            confirmationChannel = guild.GetTextChannel(settings.confirmationChannelId);
            PostConsoleLine("Got channel " + confirmationChannel.Name);
            foreach (var chan in settings.usersToNotifyIds)
            {
                usersToNotify.Add(botAPI.GetUser(chan));
            }

            return Task.CompletedTask;
        }

        async Task<Task> LogMessage(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        void PostConsoleLine(string message, bool postMessageToDiscordLog = false)
        {
            if (postMessageToDiscordLog)
            {
                foreach (var channel in usersToNotify)
                {
                    channel.SendMessageAsync(message);
                }
            }
            Console.WriteLine(DateTime.Now.ToString("H:mm:ss") + " " + message);
        }
    }
}

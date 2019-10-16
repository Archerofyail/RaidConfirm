using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using Discord.Rest;

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
        ulong guildId = 0;
        ulong confirmationChannelId = 0;
        SocketGuild guild;
        SocketTextChannel confirmationChannel;
        TimeSpan numberOfDaysAhead = TimeSpan.FromDays(7);
        static void Main(string[] args)
        {
            new Program().MainAsync().GetAwaiter().GetResult();
        }

        public async Task<Task> MainAsync()
        {

            var key = "";

            try
            {
                using (var keyFile = File.OpenText("key.txt"))
                {
                    Console.WriteLine("Grabbing info from file");
                    key = keyFile.ReadLine();
                    Console.WriteLine("key is " + key);
                    guildId = ulong.Parse(keyFile.ReadLine());
                    Console.WriteLine("guild ID is " + guildId);
                    confirmationChannelId = ulong.Parse(keyFile.ReadLine());
                    Console.WriteLine("channel ID is " + confirmationChannelId);
                    var daysAhead = int.Parse(keyFile.ReadLine());
                    numberOfDaysAhead = TimeSpan.FromDays(daysAhead);
                    Console.WriteLine("posting " + daysAhead + " days ahead of today");
                }
            }
            catch (FormatException e)
            {
                Console.WriteLine("Error parsing guild or channel IDs. Please make sure they're on their own lines with no other characters");
                return Task.CompletedTask;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error reading key file! Please make sure you have a file named key.txt in the same directory as the exe");
                return Task.CompletedTask;
            }
            await botAPI.LoginAsync(TokenType.Bot, key);
            await botAPI.StartAsync();
            botAPI.Ready += ClientReady;
            Console.WriteLine("Login Status is " + botAPI.LoginState);
            await botAPI.SetStatusAsync(UserStatus.Online);
            botAPI.ReactionAdded += ReactionAdded;
            botAPI.ReactionRemoved += ReactionRemoved;
            var timeToCheck = TimeSpan.FromMinutes(1);
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
                        Console.WriteLine("Got last " + numMessagesToGrab + " messages");
                        var messageList = messages.ToList();
                        var messageDates = new List<DateTime>(messageList.Count);
                        var index = messageList.Count - 1;
                        Console.WriteLine("Checking message for new reactions");
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
                                    foreach (var user in reactionsList)
                                    {
                                        var socketUser = guild.GetUser(user.Id);
                                        if (!names.Contains(NicknameOrFull(socketUser)) && (user.Id != botAPI.CurrentUser.Id))
                                        {
                                            hadToAddReactions = true;
                                            Console.WriteLine("Found confirmation that wasn't edited in by " + NicknameOrFull(socketUser));
                                            messageContent += "\n" + NicknameOrFull(socketUser);
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
                                            Console.WriteLine("Found warning that wasn't edited out by " + NicknameOrFull(socketUser));
                                            messageContent.Replace("\n" + NicknameOrFull(socketUser), "");
                                        }
                                    }
                                    var userCancels = await messageToConfirm.GetReactionUsersAsync(cancelEmoji, 20).FlattenAsync();
                                    foreach (var user in userCancels)
                                    {
                                        var socketUser = guild.GetUser(user.Id);
                                        if (names.Contains(NicknameOrFull(socketUser)) && (user.Id != botAPI.CurrentUser.Id))
                                        {
                                            hadToAddReactions = true;
                                            Console.WriteLine("Found X that wasn't edited out by " + NicknameOrFull(socketUser));
                                            messageContent.Replace("\n" + NicknameOrFull(socketUser), "");
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
                            Console.WriteLine("Already caught up on reactions");
                        }

                        Console.WriteLine("Checking whether to post new date");
                        if ((messageDates.Max() - numberOfDaysAhead) < DateTime.Now.Date)
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
                            while ((startDate.Date < (DateTime.Now + numberOfDaysAhead).Date))
                            {
                                startDate = startDate.AddDays(1);
                                var newDate = startDate.ToString(dateFormat);
                                var newMessage = await confirmationChannel.SendMessageAsync(newDate + "\nPeople Confirmed:\n");
                                await newMessage.AddReactionsAsync(new[] { confirmEmoji, maybeEmoji, cancelEmoji });
                            }
                        }

                        else
                        {
                            Console.WriteLine("Already caught up on messages");
                        }
                    }
                }
                System.Threading.Thread.Sleep(100);
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
                await message.ModifyAsync((x) => x.Content = oldContent.Replace("\n" + arg3.UserId, "").Replace("\n" + arg3.User.ToString(), "").Replace("\n" + ((IGuildUser)arg3.User.Value).Nickname, ""));
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
                Console.WriteLine("The emoji wasn't valid");
                if (arg3.User.IsSpecified)
                {
                    await message.RemoveReactionAsync(arg3.Emote, arg3.User.Value);
                }

                return Task.CompletedTask;
            }
            if (!oldContent.Contains(((IGuildUser)arg3.User.Value).Nickname) && arg3.Emote.Name == confirmEmoji.Name)
            {
                await message.ModifyAsync((x) => x.Content = oldContent + "\n" + ((IGuildUser)arg3.User.Value).Nickname);
            }
            var enoughPeople = message.Reactions[confirmEmoji].ReactionCount > 9;
            if (enoughPeople)
            {
                await message.AddReactionAsync(goodToGo);
            }
            return Task.CompletedTask;
        }

        private Task ClientReady()
        {
            ready = true;
            Console.WriteLine("Client is ready");
            guild = botAPI.GetGuild(guildId);
            Console.WriteLine("Got guild with name " + guild.Name);
            confirmationChannel = guild.GetTextChannel(confirmationChannelId);
            Console.WriteLine("Got channel " + confirmationChannel.Name);
            return Task.CompletedTask;
        }

        async Task<Task> LogMessage(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }
    }
}

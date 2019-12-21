using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using System.IO;
using Discord.Rest;
using Newtonsoft.Json;
using System.Threading;

namespace DiscordMessagePostBot
{
    //TODO:Make Dictionary of <messageID, users> to allow more complex message text (e.g. to list users that are maybes as well as confirmed)
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
        static async Task Main(string[] args)
        {
            await new Program().MainAsync();
        }

        async Task<bool> SaveSettings()
        {
            try
            {
                File.WriteAllText("settings.json", JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch (Exception e)
            {
                await LogMessage("Error when attempting to save settings: " + e.Message + "\nStack Trace:" + e.StackTrace);
            }
            await LogMessage("Settings saved");
            return true;
        }

        async Task<bool> LoadOrCreateSettings()
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
                        Console.WriteLine("New settings.json file created. Please close this and enter all your information into that file first");
                        Console.ReadKey();
                    }
                }
            }
            catch (Exception e)
            {
                await LogMessage("Error reading settings file! Please make sure you have a file named settings.json in the same directory as the exe");
                return false;
            }
            return true;
        }

        public async Task<Task> MainAsync()
        {
            await LogMessage("Starting up...");
            try
            {

                await LogMessage("Loaded settings...");
                if (!await LoadOrCreateSettings())
                {
                    return Task.CompletedTask;
                }
                await LogMessage("Settings are:\n" + settings.ToString());
                await botAPI.LoginAsync(TokenType.Bot, settings.key);
                await botAPI.StartAsync();
                botAPI.Ready += ClientReady;
                botAPI.Log += APILog;
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
                        if (cKey == ConsoleKey.S)
                        {
                            await SaveSettings();
                        }
                    }
                    if ((DateTime.Now) - lastCheckedTime > timeToCheck || pressedC)
                    {
                        pressedC = false;
                        lastCheckedTime = DateTime.Now;
                        if (ready)
                        {
                            var messages = await confirmationChannel.GetMessagesAsync(numMessagesToGrab).FlattenAsync();
                            await LogMessage("Got last " + numMessagesToGrab + " messages");
                            var messageList = messages.ToList();
                            var messageDates = new List<DateTime>(messageList.Count);
                            var index = messageList.Count - 1;
                            await LogMessage("Checking message for new reactions");
                            await CheckForReactions(messageList, messageDates, index);

                            await LogMessage("Checking whether to post new date");
                            await NewMessageCheck(timeToCheck, messageDates);
                        }
                    }
                    Thread.Sleep(100);
                }
            }
            catch (Exception e)
            {
                await LogMessage("Error occurred: " + e.Message + "\nStack Trace:\n" + e.StackTrace, LogDateType.DateTime, true);
                await LogMessage("Closing program");
                return Task.CompletedTask;
            }
        }

        private async Task<Task> NewMessageCheck(TimeSpan timeToCheck, List<DateTime> messageDates)
        {
            TimeSpan testSpan = new TimeSpan();
            if ((messageDates.Max() - settings.numberOfDaysAhead) < DateTime.Now.Date)
            {
                Console.Write("Most recent date is " + messageDates.Max().ToString(dateFormat));

                for (var currentDate = messageDates.Max().Date.AddDays(1); currentDate < (DateTime.Now + settings.numberOfDaysAhead).Date.AddDays(1); currentDate = currentDate.AddDays(1))
                {
                    var time = settings.raidTimes.FirstOrDefault(x => x.Item1 == currentDate.DayOfWeek)?.Item2;
                    if (time.HasValue && time != default(TimeSpan))
                    {
                        var messageDate = (currentDate + time.Value).ToString(dateFormat);
                        var newMessage = await confirmationChannel.SendMessageAsync(messageDate + "\nPeople Confirmed:");
                        await newMessage.AddReactionsAsync(new[] { confirmEmoji, maybeEmoji, cancelEmoji });
                    }

                }
            }

            else
            {
                await LogMessage("Already caught up on messages, waiting " + timeToCheck.ToString("%m") + " minutes before checking again");
            }

            return Task.CompletedTask;
        }

        private async Task<Task> CheckForReactions(List<IMessage> messageList, List<DateTime> messageDates, int index)
        {
            try
            {
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
                            var namesToAdd = new List<string>();
                            var namesToRemove = new List<string>();
                            var confirmedNames = await CheckIfUserReacted(userConfirmations, names, true);
                            if (userConfirmations.Count() > 8)
                            {
                                await messageToConfirm.AddReactionAsync(goodToGo);
                            }
                            else if (messageToConfirm.Reactions.ContainsKey(goodToGo))
                            {
                                await messageToConfirm.RemoveReactionAsync(goodToGo, botAPI.CurrentUser);
                            }
                            foreach (var userName in confirmedNames)
                            {
                                namesToAdd.Add(userName);
                            }
                            Thread.Sleep(100);

                            var userWarnings = await messageToConfirm.GetReactionUsersAsync(maybeEmoji, 20).FlattenAsync();
                            var warningNames = await CheckIfUserReacted(userWarnings, names);
                            foreach (var userName in warningNames)
                            {
                                namesToRemove.Add(userName);
                            }
                            Thread.Sleep(100);
                            var userCancels = await messageToConfirm.GetReactionUsersAsync(cancelEmoji, 20).FlattenAsync();
                            var cancelNames = await CheckIfUserReacted(userCancels, names);
                            foreach (var userName in cancelNames)
                            {
                                namesToRemove.Add(userName);
                            }
                            hadToAddReactions = namesToAdd.Count > 0 || namesToRemove.Count > 0;
                            for (int i1 = 0; i1 < namesToRemove.Count; i1++)
                            {
                                string name = namesToRemove[i1];
                                if (userConfirmations.Any(x => name == ("\n**" + NicknameOrFull(guild.GetUser(x.Id)) + "**")))
                                {
                                    namesToRemove.RemoveAt(i1);
                                    i1--;
                                }
                            }
                            foreach (var name in namesToRemove)
                            {
                                messageContent = messageContent.Replace(name, "");
                            }
                            foreach (var name in namesToAdd)
                            {
                                messageContent += name;
                            }

                            var splits = messageContent.Split('\n');
                            var uniques = splits.Distinct();
                            messageContent = "";
                            var i = 0;
                            foreach (var str in uniques)
                            {
                                if (i > 0)
                                {
                                    messageContent += "\n" + str;
                                }
                                else
                                {
                                    messageContent += str;
                                }
                                i++;
                            }
                            await messageToConfirm.ModifyAsync(x => x.Content = messageContent);
                        }
                    }
                    else
                    {
                        messageDates.Add(DateTime.MinValue + TimeSpan.FromDays(50));
                    }
                    index--;
                    Thread.Sleep(100);
                }
                if (!hadToAddReactions)
                {
                    await LogMessage("Caught up on reactions");
                }

            }
            catch (Exception e)
            {
                await LogMessage("Error Occurred: " + e.Message + "\n" + e.StackTrace);
            }

            return Task.CompletedTask;
        }

        async Task<List<string>> CheckIfUserReacted(IEnumerable<IUser> users, IEnumerable<string> names, bool isAdding = false)
        {
            var namesToModify = new List<string>();
            foreach (var user in users)
            {
                var socketUser = guild.GetUser(user.Id);
                var addRemoveName = names.Contains("**" + NicknameOrFull(socketUser) + "**") && (user.Id != botAPI.CurrentUser.Id);
                var addAddName = !names.Contains("**" + NicknameOrFull(socketUser) + "**") && (user.Id != botAPI.CurrentUser.Id);
                var shouldAddName = isAdding ? addAddName : addRemoveName;
                if (shouldAddName)
                {
                    await LogMessage("Found " + (isAdding ? "confirmation" : "X or warning") + " that wasn't edited by " + NicknameOrFull(socketUser));
                    namesToModify.Add("\n**" + NicknameOrFull(socketUser) + "**");
                }
            }
            return namesToModify;
        }

        private string NicknameOrFull(IGuildUser user)
        {
            return !string.IsNullOrEmpty(user.Nickname) ? user.Nickname : user.Username.Split('#')[0];
        }

        private async Task<Task> ReactionRemoved(Cacheable<IUserMessage, ulong> messagePromise, ISocketMessageChannel channel, SocketReaction reaction)
        {
            var message = await messagePromise.GetOrDownloadAsync();
            if (message.Author.Id != botAPI.CurrentUser.Id)
            { return Task.CompletedTask; }
            var enoughPeople = message.Reactions[confirmEmoji].ReactionCount < 9;
            var oldContent = message.Content;
            if (reaction.Emote.Name == confirmEmoji.Name)
            {
                await message.ModifyAsync((x) => x.Content = oldContent.Replace("\n**" + ((IGuildUser)reaction.User.Value).Nickname + "**", ""));
            }

            if (enoughPeople && message.Reactions.ContainsKey(goodToGo))
            {
                await message.RemoveReactionAsync(goodToGo, botAPI.CurrentUser);
            }
            return Task.CompletedTask;
        }

        private async Task<Task> ReactionAdded(Cacheable<IUserMessage, ulong> messagePromise, ISocketMessageChannel channel, SocketReaction reaction)
        {

            var message = await messagePromise.GetOrDownloadAsync();

            if (message.Author.Id != botAPI.CurrentUser.Id)
            { return Task.CompletedTask; }
            if (reaction.User.IsSpecified && reaction.UserId == botAPI.CurrentUser.Id)
            { return Task.CompletedTask; }
            var oldContent = message.Content;

            if (reaction.Emote.Name != confirmEmoji.Name && reaction.Emote.Name != maybeEmoji.Name && reaction.Emote.Name != cancelEmoji.Name)
            {
                await LogMessage("The emoji wasn't valid");
                if (reaction.User.IsSpecified)
                {
                    await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);
                }

                return Task.CompletedTask;
            }

            if (reaction.User.IsSpecified && reaction.User.Value != null && !oldContent.Contains(NicknameOrFull((SocketGuildUser)reaction.User.Value)) && reaction.Emote.Name == confirmEmoji.Name)
            {
                await LogMessage("Reaction added to comment from user " + reaction.User.Value);
                await message.ModifyAsync((x) => x.Content = oldContent + "\n**" + (NicknameOrFull((SocketGuildUser)reaction.User.Value) + "**"));
            }
            else
            {
                await LogMessage("User is " + (reaction.User.IsSpecified ? "specified" : "not specified, ") + "and is " + reaction.User.Value);
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
            await LogMessage("Login Status is " + botAPI.LoginState);
            await botAPI.SetStatusAsync(UserStatus.Online);
            await LogMessage("Client is ready");
            guild = botAPI.GetGuild(settings.guildId);
            await LogMessage("Got guild with name " + guild.Name);
            confirmationChannel = guild.GetTextChannel(settings.confirmationChannelId);
            await LogMessage("Got channel " + confirmationChannel.Name);
            foreach (var chan in settings.usersToNotifyIds)
            {
                usersToNotify.Add(botAPI.GetUser(chan));
            }

            return Task.CompletedTask;
        }

        async Task<Task> APILog(LogMessage log)
        {
            await LogMessage(log.ToString(), LogDateType.DateOnly);
            return Task.CompletedTask;
        }

        async Task LogMessage(string message, LogDateType prependDate = LogDateType.DateTime, bool postMessageToDiscordLog = false)
        {
            using (var logFile = new StreamWriter("log.txt", true))
            {
                if (postMessageToDiscordLog)
                {
                    foreach (var channel in usersToNotify)
                    {
                        await channel.SendMessageAsync(message);
                    }
                }
                string logMessage = message;
                switch (prependDate)
                {
                    case (LogDateType.DateOnly):
                        {
                            logMessage = logMessage.Insert(0, DateTime.Now.ToString("MMMM dd -") + " ");
                            break;

                        }
                    case (LogDateType.DateTime):
                        {
                            logMessage = logMessage.Insert(0, DateTime.Now.ToString("MMMM dd - H:mm:ss") + " ");
                            break;
                        }

                }
                Console.WriteLine(logMessage);
                await logFile.WriteLineAsync(logMessage);
            }
        }
    }
    public static class DateTimeExtensions
    {
        public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek)
        {
            int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
            return dt.AddDays(-1 * diff).Date;
        }
    }

    public enum LogDateType
    {
        None,
        DateOnly,
        DateTime
    }
}

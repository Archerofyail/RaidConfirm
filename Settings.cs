using System;
using System.Collections.Generic;

namespace DiscordMessagePostBot
{
    class Settings
    {
        public string key;
        public string dateFormat = "dddd MMMM d, h:m tt";
        public int numMessagesToGrab = 10;
        public ulong guildId = 0;
        public ulong confirmationChannelId = 0;
        public List<ulong> usersToNotifyIds = new List<ulong>();
        public TimeSpan numberOfDaysAhead = TimeSpan.FromDays(7);
        public Tuple<DayOfWeek, TimeSpan>[] raidTimes = new Tuple<DayOfWeek, TimeSpan>[7];

        public Settings()
        {
            raidTimes = new Tuple<DayOfWeek, TimeSpan>[]{Tuple.Create(DayOfWeek.Sunday, TimeSpan.FromHours(18.5f) ), 
                Tuple.Create(DayOfWeek.Monday, TimeSpan.FromHours(18.5f)),
                Tuple.Create(DayOfWeek.Tuesday, TimeSpan.FromHours(18.5f)),
                Tuple.Create(DayOfWeek.Wednesday, TimeSpan.FromHours(18.5f)),
                Tuple.Create(DayOfWeek.Thursday, TimeSpan.FromHours(18.5f)),
                Tuple.Create(DayOfWeek.Friday, TimeSpan.FromHours(18.5f)),
                Tuple.Create(DayOfWeek.Saturday, TimeSpan.FromHours(18.5f))};
        }

        public override string ToString()
        {
            string finalstring = "";
            finalstring += "Key is " + key + "\n";
            finalstring += "Date Format is " + dateFormat + "\n";
            finalstring += "Number of messages to check is " + numMessagesToGrab + "\n";
            finalstring += "Number of days ahead is " + numberOfDaysAhead + "\n";
            finalstring += "Guild ID is " + guildId + "\n";
            finalstring += "Channel to post in is " + confirmationChannelId + "\n";
            finalstring += "Users to notify about errors are " + usersToNotifyIds.ToString() + "\n";
            finalstring += "Raid Times are " + "\n";
            foreach (var raidTime in raidTimes)
            {
                finalstring += raidTime.Item1 + ", " + raidTime.Item2.ToString() + ";\n";
            }
            return finalstring;
        }
    }
}

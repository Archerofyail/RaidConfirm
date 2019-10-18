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
    }
}

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
        public TimeSpan[] raidTimes = new TimeSpan[7];

        public Settings()
        {
            raidTimes = new TimeSpan[]{
                TimeSpan.FromHours(18.5f), TimeSpan.FromHours(42.5f),
                TimeSpan.FromHours(66.5f),TimeSpan.FromHours(90.5f),
                TimeSpan.FromHours(114.5f), TimeSpan.FromHours(138.5),
                TimeSpan.FromHours(162.5f)};
        }
    }
}

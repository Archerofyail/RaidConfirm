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
    }
}

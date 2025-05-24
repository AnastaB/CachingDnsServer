using System;

namespace DnsServerGUI
{
    public static class Logger
    {
        public static event Action<LogEntry> LogUpdated;

        public static void Log(string type, string domain)
        {
            var entry = new LogEntry
            {
                Time = DateTime.Now,
                EventType = type,
                Domain = domain
            };

            LogUpdated?.Invoke(entry);
        }
    }
}

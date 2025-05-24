using System;
using System.Collections.Generic;


namespace DnsServerGUI
{
    public class LogEntry
    {
        public string EventType { get; set; }
        public string Domain { get; set; }
        public string Message { get; set; }
        public DateTime Time { get; set; }
    }

    public class Logger
    {
        private readonly List<string> _logEntries = new List<string>();

        public IReadOnlyList<string> Entries => _logEntries.AsReadOnly();

        public event Action<LogEntry> LogUpdated;

        public void Log(string eventType, string domain, string message = "")
        {
            var timestamp = DateTime.Now;
            var line = $"[{timestamp:yyyy-MM-dd HH:mm:ss}] [{eventType}] {domain} {message}";
            _logEntries.Add(line.Trim());
            Console.WriteLine(line);

            LogUpdated?.Invoke(new LogEntry
            {
                EventType = eventType,
                Domain = domain,
                Message = message,
                Time = timestamp
            });
        }
    }
}

using System;
using System.Collections.Generic;

namespace DnsServerGUI
{
    public static class DnsCache
    {
        public static event Action<List<CacheEntry>> CacheChanged;

        private static List<CacheEntry> currentCache = new List<CacheEntry>();

        public static void SimulateUpdate()
        {
            currentCache = new List<CacheEntry>
            {
                new CacheEntry { Domain = "example.com", IP = "93.184.216.34", TTL = 300, Timestamp = DateTime.Now },
                new CacheEntry { Domain = "google.com", IP = "142.250.190.78", TTL = 200, Timestamp = DateTime.Now }
            };

            CacheChanged?.Invoke(currentCache);
        }
    }
}

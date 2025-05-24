using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;

namespace DnsServerGUI
{
    public class CacheEntry
    {
        public string Domain { get; set; }
        public DnsResourceRecord[] Records { get; set; }
        public int TTL { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DnsCache
    {
        private class InternalEntry
        {
            public DnsResourceRecord[] Records { get; set; }
            public int Ttl { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private readonly Dictionary<string, InternalEntry> _cache = new Dictionary<string, InternalEntry>();
        private readonly Timer _cleanupTimer;

        public event Action<List<CacheEntry>> CacheChanged;

        public DnsCache()
        {
            _cleanupTimer = new Timer(10 * 1000);
            _cleanupTimer.Elapsed += (s, e) => Cleanup();
            _cleanupTimer.Start();
        }

        public bool TryGet(string domain, out DnsResourceRecord[] records)
        {
            lock (_cache)
            {
                if (_cache.ContainsKey(domain))
                {
                    var entry = _cache[domain];
                    if ((DateTime.UtcNow - entry.CreatedAt).TotalSeconds <= entry.Ttl)
                    {
                        records = entry.Records;
                        return true;
                    }
                    else
                    {
                        _cache.Remove(domain);
                        RaiseChanged();
                    }
                }
            }
            records = null;
            return false;
        }

        public void Add(string domain, DnsResourceRecord[] records)
        {
            int ttl = records.Length > 0 ? (int)records.Min(r => r.TTL) : 300;

            lock (_cache)
            {
                _cache[domain] = new InternalEntry
                {
                    Records = records,
                    Ttl = ttl,
                    CreatedAt = DateTime.UtcNow
                };
                RaiseChanged();
            }
        }

        private void Cleanup()
        {
            lock (_cache)
            {
                var toRemove = _cache.Where(kvp =>
                    (DateTime.UtcNow - kvp.Value.CreatedAt).TotalSeconds > kvp.Value.Ttl
                ).Select(kvp => kvp.Key).ToList();

                foreach (var key in toRemove)
                {
                    _cache.Remove(key);
                }

                if (toRemove.Count > 0)
                    RaiseChanged();
            }
        }

        private void RaiseChanged()
        {
            CacheChanged?.Invoke(GetSnapshot());
        }

        public List<CacheEntry> GetSnapshot()
        {
            lock (_cache)
            {
                return _cache.Select(kvp => new CacheEntry
                {
                    Domain = kvp.Key,
                    Records = kvp.Value.Records,
                    TTL = kvp.Value.Ttl,
                    Timestamp = kvp.Value.CreatedAt
                }).ToList();
            }
        }
    }
}
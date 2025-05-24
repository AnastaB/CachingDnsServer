using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace DnsServerGUI
{
    public class BlacklistFilter
    {
        private readonly List<string> _rawPatterns = new List<string>();
        private readonly List<Regex> _compiledPatterns = new List<Regex>();
        private readonly string _blacklistFile;

        public event Action<List<string>> BlacklistUpdated;

        public BlacklistFilter(string blacklistFile)
        {
            _blacklistFile = blacklistFile;
            Load();
        }

        public void Load()
        {
            if (!File.Exists(_blacklistFile))
                return;

            _rawPatterns.Clear();
            _compiledPatterns.Clear();

            string[] lines = File.ReadAllLines(_blacklistFile);
            foreach (string line in lines)
            {
                string pattern = line.Trim();
                if (string.IsNullOrEmpty(pattern) || pattern.StartsWith("#"))
                    continue;

                _rawPatterns.Add(pattern);
                string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
                _compiledPatterns.Add(new Regex(regexPattern, RegexOptions.IgnoreCase));
            }

            BlacklistUpdated?.Invoke(GetPatterns());
        }

        public bool IsBlocked(string domain)
        {
            foreach (Regex regex in _compiledPatterns)
            {
                if (regex.IsMatch(domain))
                    return true;
            }

            return false;
        }

        public void Reload()
        {
            Load();
        }

        public List<string> GetPatterns()
        {
            return _rawPatterns.ToList();
        }
    }
}

using System;
using System.Drawing;
using System.Windows.Forms;
using DnsServerGUI; 


namespace DnsServerGUI
{
    public static class BlacklistFilter
    {
        public static event Action<List<string>> BlacklistUpdated;

        private static string blacklistPath = "blacklist.txt";

        public static void Reload()
        {
            var list = new List<string>();
            if (File.Exists(blacklistPath))
            {
                list.AddRange(File.ReadAllLines(blacklistPath));
            }

            BlacklistUpdated?.Invoke(list);
        }
    }
}

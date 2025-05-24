using System;
using System.Drawing;
using System.Windows.Forms;
using DnsServerGUI; 


namespace DnsServerGUI
{
    public class UIBindings
    {
        private readonly MainForm form;

        public UIBindings(MainForm mainForm)
        {
            this.form = mainForm;

            Logger.LogUpdated += OnLogUpdated;
            DnsCache.CacheChanged += OnCacheChanged;
            BlacklistFilter.BlacklistUpdated += OnBlacklistUpdated;
        }

        private void OnLogUpdated(LogEntry entry)
        {
            var grid = form.GetLogGridView();
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action(() => AddLogRow(grid, entry)));
            }
            else
            {
                AddLogRow(grid, entry);
            }
        }

        private void AddLogRow(DataGridView grid, LogEntry entry)
        {
            grid.Rows.Add(entry.Time.ToString("T"), entry.EventType, entry.Domain);
        }

        private void OnCacheChanged(List<CacheEntry> cache)
        {
            var grid = form.GetCacheGridView();
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action(() => UpdateCacheGrid(grid, cache)));
            }
            else
            {
                UpdateCacheGrid(grid, cache);
            }
        }

        private void UpdateCacheGrid(DataGridView grid, List<CacheEntry> cache)
        {
            grid.Rows.Clear();
            foreach (var entry in cache)
            {
                grid.Rows.Add(entry.Domain, entry.IP, entry.TTL, entry.Timestamp.ToString("T"));
            }
        }

        private void OnBlacklistUpdated(List<string> domains)
        {
            var grid = form.GetBlacklistGridView();
            if (grid.InvokeRequired)
            {
                grid.Invoke(new Action(() => UpdateBlacklistGrid(grid, domains)));
            }
            else
            {
                UpdateBlacklistGrid(grid, domains);
            }
        }

        private void UpdateBlacklistGrid(DataGridView grid, List<string> domains)
        {
            grid.Rows.Clear();
            foreach (var domain in domains)
            {
                if (!string.IsNullOrWhiteSpace(domain))
                    grid.Rows.Add(domain.Trim());
            }
        }
    }
}

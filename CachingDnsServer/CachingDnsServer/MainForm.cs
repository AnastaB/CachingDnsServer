using System;
using System.Drawing;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DnsServerGUI
{
    public partial class MainForm : Form
    {
        private Logger logger;
        private DnsCache dnsCache;
        private BlacklistFilter blacklistFilter;

        private DnsServer dnsServer;
        private CancellationTokenSource serverCts;

        private Button startButton;
        private Button stopButton;
        private Button editBlacklistButton;
        private Button reloadBlacklistButton;

        private DataGridView cacheGridView;
        private DataGridView logGridView;
        private DataGridView blacklistGridView;

        public DataGridView GetCacheGridView() => cacheGridView;
        public DataGridView GetLogGridView() => logGridView;
        public DataGridView GetBlacklistGridView() => blacklistGridView;

        public MainForm()
        {
            logger = new Logger();
            dnsCache = new DnsCache();
            blacklistFilter = new BlacklistFilter("blacklist.txt");

            InitializeComponents();
            _ = new UIBindings(this, logger, dnsCache, blacklistFilter);
        }

        private void InitializeComponents()
        {
            this.Text = "Кэширующий DNS-сервер";
            this.WindowState = FormWindowState.Maximized;
            this.MinimumSize = new Size(1000, 700);
            this.BackColor = Color.White;

            cacheGridView = CreateGrid("Кэш доменов");
            logGridView = CreateGrid("Логи событий");
            blacklistGridView = CreateGrid("Чёрный список");

            startButton = CreateButton("Старт сервера", StartButton_Click);
            stopButton = CreateButton("Стоп сервера", StopButton_Click);
            editBlacklistButton = CreateButton("Редактировать Чёрный список", EditBlacklistButton_Click, 250);
            reloadBlacklistButton = CreateButton("Обновить Чёрный список", ReloadBlacklistButton_Click, 200);

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Top,
                Padding = new Padding(10),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Color.White
            };
            buttonPanel.Controls.AddRange(new Control[]
            {
                startButton, stopButton, editBlacklistButton, reloadBlacklistButton
            });

            var testPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                Dock = DockStyle.Bottom,
                Padding = new Padding(10),
                AutoSize = true,
                BackColor = Color.White
            };

            var inputBox = new TextBox
            {
                Width = 300,
                Font = new Font("Segoe UI", 11)
            };

            var checkButton = new Button
            {
                Text = "Проверить DNS",
                Height = 35,
                Width = 150
            };

            var resultLabel = new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI", 11),
                Padding = new Padding(10, 5, 0, 0)
            };

            checkButton.Click += async (s, e) =>
            {
                string domain = inputBox.Text.Trim().ToLower();

                if (string.IsNullOrWhiteSpace(domain))
                {
                    resultLabel.Text = "Введите домен.";
                    return;
                }

                if (blacklistFilter.IsBlocked(domain))
                {
                    resultLabel.Text = "Заблокировано фильтром (черный список).";
                    logger.Log("BLOCKED", domain, "Проверка вручную");
                    return;
                }

                if (dnsCache.TryGet(domain, out DnsResourceRecord[] cachedRecords))
                {
                    var ip = new IPAddress(cachedRecords[0].RData).ToString();
                    resultLabel.Text = $"Из кэша: {ip}";
                    logger.Log("CACHED", domain, "Проверка вручную");
                    return;
                }

                try
                {
                    var forwarder = new DnsForwarder("8.8.8.8");
                    byte[] query = DnsPacketParser.BuildQuery(domain);
                    byte[] response = await forwarder.ForwardRequestAsync(query);

                    if (response == null)
                    {
                        resultLabel.Text = "Нет ответа от DNS.";
                        return;
                    }

                    var parsed = DnsPacketParser.ParseResponse(response);
                    var answers = parsed.Item3;

                    if (answers.Count > 0 && answers[0].Type == 1)
                    {
                        var ip = new IPAddress(answers[0].RData).ToString();
                        resultLabel.Text = $"Ответ DNS: {ip}";
                        dnsCache.Add(domain, answers.ToArray());
                        logger.Log("FORWARD", domain, $"Результат: {ip}");
                    }
                    else
                    {
                        resultLabel.Text = "Нет IP-ответа.";
                    }
                }
                catch (Exception ex)
                {
                    resultLabel.Text = $"Ошибка: {ex.Message}";
                    logger.Log("ERROR", domain, ex.Message);
                }
            };

            testPanel.Controls.Add(inputBox);
            testPanel.Controls.Add(checkButton);
            testPanel.Controls.Add(resultLabel);

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                Padding = new Padding(15),
                BackColor = Color.White
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            mainLayout.Controls.Add(CreateLabeledGroup("Кэш доменов", cacheGridView), 0, 0);
            mainLayout.Controls.Add(CreateLabeledGroup("Логи", logGridView), 0, 1);
            mainLayout.Controls.Add(CreateLabeledGroup("Чёрный список", blacklistGridView), 0, 2);

            this.Controls.Clear();
            this.Controls.Add(mainLayout);
            this.Controls.Add(buttonPanel);
            this.Controls.Add(testPanel);
        }

        private Button CreateButton(string text, EventHandler handler, int width = 150)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 35
            };
            button.Click += handler;
            return button;
        }

        private DataGridView CreateGrid(string type)
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                MultiSelect = false
            };

            switch (type)
            {
                case "Кэш доменов":
                    grid.Columns.Add("Domain", "Домен");
                    grid.Columns.Add("IP", "IP-адрес");
                    grid.Columns.Add("TTL", "TTL");
                    grid.Columns.Add("Added", "Время добавления");
                    break;
                case "Логи событий":
                    grid.Columns.Add("Time", "Время");
                    grid.Columns.Add("Event", "Событие");
                    grid.Columns.Add("Domain", "Домен");
                    break;
                case "Чёрный список":
                    grid.Columns.Add("Domain", "Заблокированный домен");
                    break;
            }

            return grid;
        }

        private GroupBox CreateLabeledGroup(string title, Control content)
        {
            return new GroupBox
            {
                Text = title,
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Controls = { content }
            };
        }

        private async void StartButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (dnsServer != null)
                {
                    MessageBox.Show("Сервер уже запущен.");
                    return;
                }

                serverCts = new CancellationTokenSource();
                dnsServer = new DnsServer(53, "8.8.8.8", dnsCache, blacklistFilter, logger);
                _ = Task.Run(() => dnsServer.StartAsync(serverCts.Token));
                logger.Log("INFO", "UI", "Сервер запущен.");
                MessageBox.Show("Сервер запущен.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при запуске сервера: " + ex.Message);
                logger.Log("ERROR", "UI", ex.ToString());
            }
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (serverCts == null)
                {
                    MessageBox.Show("Сервер не был запущен.");
                    return;
                }

                serverCts.Cancel();
                dnsServer = null;
                serverCts = null;
                logger.Log("INFO", "UI", "Сервер остановлен.");
                MessageBox.Show("Сервер остановлен.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при остановке сервера: " + ex.Message);
                logger.Log("ERROR", "UI", ex.ToString());
            }
        }

        private void EditBlacklistButton_Click(object sender, EventArgs e)
        {
            using (var editor = new BlacklistEditorForm())
            {
                editor.ShowDialog();
            }
        }

        private void ReloadBlacklistButton_Click(object sender, EventArgs e)
        {
            blacklistFilter.Reload();
        }
    }
}
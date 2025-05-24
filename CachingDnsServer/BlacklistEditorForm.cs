using System;
using System.Drawing;
using System.Windows.Forms;
using DnsServerGUI;


namespace DnsServerGUI
{
    public class BlacklistEditorForm : Form
    {
        private TextBox blacklistTextBox;
        private Button saveButton;
        private Button cancelButton;

        private readonly string blacklistPath = "blacklist.txt";

        public BlacklistEditorForm()
        {
            InitializeComponents();
            LoadBlacklist();
        }

        private void InitializeComponents()
        {
            this.Text = "Редактор черного списка";
            this.Size = new System.Drawing.Size(600, 500);
            this.StartPosition = FormStartPosition.CenterParent;

            blacklistTextBox = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font("Consolas", 11)
            };

            saveButton = new Button
            {
                Text = "Сохранить",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "Отмена",
                Dock = DockStyle.Bottom,
                Height = 40
            };
            cancelButton.Click += (s, e) => this.Close();

            this.Controls.Add(blacklistTextBox);
            this.Controls.Add(saveButton);
            this.Controls.Add(cancelButton);
        }

        private void LoadBlacklist()
        {
            if (File.Exists(blacklistPath))
            {
                blacklistTextBox.Text = File.ReadAllText(blacklistPath);
            }
            else
            {
                blacklistTextBox.Text = "";
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            File.WriteAllText(blacklistPath, blacklistTextBox.Text);
            MessageBox.Show("Список сохранён.");
            this.Close();
        }
    }
}

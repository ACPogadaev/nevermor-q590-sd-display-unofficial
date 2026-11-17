using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: AssemblyTitle("Nevermor HID Keyboard Switch")]
[assembly: AssemblyDescription("Управление клавиатурными HID-записями Nevermor Q590-SD")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace NevermorKeyboardSwitch
{
    internal static class Program
    {
        [STAThread]
        internal static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SwitchWindow());
        }
    }

    internal sealed class SwitchWindow : Form
    {
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(506, 0) };
        private readonly ListView entries = new ListView
        {
            Dock = DockStyle.Fill, View = View.Details, HeaderStyle = ColumnHeaderStyle.Nonclickable,
            FullRowSelect = true, MultiSelect = false, TabStop = false
        };
        private readonly Button disable = new Button { Text = "Отключить записи", AutoSize = true };
        private readonly Button enable = new Button { Text = "Включить обратно", AutoSize = true };
        private readonly Button refresh = new Button { Text = "Обновить", AutoSize = true };
        private bool busy;

        internal SwitchWindow()
        {
            Text = "Nevermor HID Keyboard Switch";
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(546, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var title = new Label
            {
                Text = "Клавиатурные записи Q590-SD",
                Font = new Font(Font.FontFamily, 15, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 10)
            };
            status.Margin = new Padding(0, 0, 0, 10);
            entries.Margin = new Padding(0, 0, 0, 12);
            entries.Columns.Add("Коллекция HID", 260);
            entries.Columns.Add("Состояние", 212);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Margin = Padding.Empty };
            actions.Controls.AddRange(new Control[] { disable, enable, refresh });
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.Controls.Add(title, 0, 0);
            layout.Controls.Add(status, 0, 1);
            layout.Controls.Add(entries, 0, 2);
            layout.Controls.Add(actions, 0, 3);
            Controls.Add(layout);

            disable.Enabled = false;
            enable.Enabled = false;
            Shown += async (sender, args) => await RefreshState(null);
            refresh.Click += async (sender, args) => await RefreshState(null);
            disable.Click += async (sender, args) => await RefreshState(false);
            enable.Click += async (sender, args) => await RefreshState(true);
            FormClosing += (sender, args) => args.Cancel = busy && args.CloseReason == CloseReason.UserClosing;
        }

        private async Task RefreshState(bool? requestedState)
        {
            busy = true;
            disable.Enabled = enable.Enabled = refresh.Enabled = false;
            status.Text = requestedState.HasValue ? "Меняю состояние записей…" : "Ищу дисплей…";
            try
            {
                string detail = null;
                if (requestedState.HasValue)
                    detail = await Task.Run(() => Devices.Change(requestedState.Value));

                DeviceScan scan = await Task.Run(() => Devices.Scan());
                entries.Items.Clear();
                foreach (Collection keyboard in scan.Keyboards)
                    entries.Items.Add(new ListViewItem(new[] { keyboard.Name, Devices.StateText(keyboard.State) }));

                if (scan.Message == null)
                {
                    int inactive = scan.Keyboards.Count(item => item.State == DeviceState.Disabled);
                    status.Text = string.Format("HID-канал дисплея активен. Отключено записей Windows: {0} из {1}.",
                        inactive, scan.Keyboards.Length);
                    disable.Enabled = scan.Keyboards.Any(item => item.State == DeviceState.Started);
                }
                else
                {
                    status.Text = scan.Message;
                }
                enable.Enabled = scan.Complete && scan.Keyboards.Any(item => item.State == DeviceState.Disabled);

                if (requestedState.HasValue && detail == null && scan.Complete &&
                    scan.Keyboards.Any(item => item.State != (requestedState.Value ? DeviceState.Started : DeviceState.Disabled)))
                    detail = "Windows ещё не подтвердила новое состояние всех записей. Нажмите «Обновить».";
                if (detail != null)
                    MessageBox.Show(this, detail, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception error)
            {
                status.Text = "Ошибка Windows. Нажмите «Обновить», чтобы повторить проверку.";
                MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                refresh.Enabled = true;
            }
        }
    }
}

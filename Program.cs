using System.Drawing;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

ApplicationConfiguration.Initialize();
Application.Run(new MainForm());

class MainForm : Form
{
    readonly ListBox list = new() { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 12), IntegralHeight = false };
    readonly Button btnRefresh = new() { Text = "تازه‌سازی لیست", Dock = DockStyle.Fill };
    readonly Button btnConnect = new() { Text = "اتصال", Dock = DockStyle.Fill };
    readonly Button btnDisconnect = new() { Text = "قطع اتصال", Dock = DockStyle.Fill };
    readonly Label status = new() { Dock = DockStyle.Bottom, Height = 52, Padding = new Padding(8), ForeColor = Color.DimGray };

    List<DeviceInformation> devices = new();
    readonly Dictionary<string, AudioPlaybackConnection> conns = new();

    public MainForm()
    {
        Text = "گیرنده صدای بلوتوث";
        Width = 460;
        Height = 480;
        StartPosition = FormStartPosition.CenterScreen;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10);

        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 56,
            Padding = new Padding(8),
            Text = "اول گوشی را از تنظیمات ویندوز با سیستم جفت کنید، بعد آن را از لیست انتخاب و «اتصال» بزنید."
        };

        var buttons = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 52, ColumnCount = 3, RowCount = 1 };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        buttons.Controls.Add(btnConnect, 0, 0);
        buttons.Controls.Add(btnDisconnect, 1, 0);
        buttons.Controls.Add(btnRefresh, 2, 0);

        Controls.Add(list);
        Controls.Add(buttons);
        Controls.Add(status);
        Controls.Add(hint);

        btnRefresh.Click += async (_, _) => await LoadDevices();
        btnConnect.Click += async (_, _) => await ConnectSelected();
        btnDisconnect.Click += (_, _) => DisconnectSelected();
        Shown += async (_, _) => await LoadDevices();
        FormClosing += (_, _) =>
        {
            foreach (var c in conns.Values) c.Dispose();
            conns.Clear();
        };
    }

    void SetStatus(string text) => status.Text = text;

    async Task LoadDevices()
    {
        try
        {
            SetStatus("در حال جستجو...");
            var found = await DeviceInformation.FindAllAsync(AudioPlaybackConnection.GetDeviceSelector());
            devices = found.ToList();
            Render();
            SetStatus(devices.Count == 0
                ? "دستگاهی پیدا نشد. مطمئن شوید گوشی با سیستم جفت شده و بلوتوث روشن است."
                : $"{devices.Count} دستگاه پیدا شد.");
        }
        catch (Exception ex)
        {
            SetStatus("خطا: " + ex.Message);
        }
    }

    void Render()
    {
        int sel = list.SelectedIndex;
        list.Items.Clear();
        foreach (var d in devices)
        {
            string st = "";
            if (conns.TryGetValue(d.Id, out var c))
                st = c.State == AudioPlaybackConnectionState.Opened ? "   ● متصل" : "   ○ آماده (از گوشی وصل شوید)";
            list.Items.Add(d.Name + st);
        }
        if (sel >= 0 && sel < list.Items.Count) list.SelectedIndex = sel;
    }

    async Task ConnectSelected()
    {
        int i = list.SelectedIndex;
        if (i < 0) { SetStatus("اول یک دستگاه انتخاب کنید."); return; }
        var d = devices[i];

        try
        {
            if (!conns.TryGetValue(d.Id, out var conn))
            {
                conn = AudioPlaybackConnection.TryCreateFromId(d.Id);
                if (conn == null) { SetStatus("ساخت اتصال ممکن نشد."); return; }
                conn.StateChanged += (_, _) => BeginInvoke(new Action(Render));
                conns[d.Id] = conn;
            }

            SetStatus("در حال اتصال...");
            await conn.StartAsync();
            var res = await conn.OpenAsync();

            if (res.Status == AudioPlaybackConnectionOpenResultStatus.Success)
            {
                SetStatus("متصل شد. حالا روی گوشی صدا پخش کنید.");
            }
            else
            {
                SetStatus("اتصال انجام نشد (" + res.Status + "). از گوشی هم می‌توانید به سیستم وصل شوید.");
            }
            Render();
        }
        catch (Exception ex)
        {
            SetStatus("خطا: " + ex.Message);
        }
    }

    void DisconnectSelected()
    {
        int i = list.SelectedIndex;
        if (i < 0) return;
        var d = devices[i];
        if (conns.Remove(d.Id, out var c))
        {
            c.Dispose();
            SetStatus("قطع شد.");
            Render();
        }
    }
}

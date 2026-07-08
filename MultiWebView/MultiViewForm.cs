using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;

namespace MultiWebView;

public sealed class MultiViewForm : Form
{
    private const int TitleBarHeight = 36;

    private readonly IReadOnlyList<Profile> profiles;
    private readonly ProfileStore profileStore;
    private readonly List<WebView2> webViews = [];
    private readonly Color btnNormal = Color.FromArgb(28, 28, 28);
    private readonly Color btnHover = Color.FromArgb(60, 60, 60);
    private readonly Color btnCloseHover = Color.FromArgb(232, 17, 35);

    private Button btnMax = null!;
    private Rectangle previousBounds;
    private bool isMaximized;

    [DllImport("user32.dll")]
    private static extern void ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern void SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WmNcButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    public MultiViewForm(IReadOnlyList<Profile> profiles, ProfileStore profileStore)
    {
        this.profiles = profiles;
        this.profileStore = profileStore;

        Text = "Multi WebView";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        BackColor = Color.FromArgb(20, 20, 20);
        MinimumSize = new Size(1000, 650);
        Size = new Size(1400, 900);
        SetFormIcon(this);

        BuildTitleBar();
        BuildGrid();

        Shown += async (_, _) =>
        {
            ToggleMaximize();
            await InitializeWebViewsAsync();
        };
    }

    private void BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Height = TitleBarHeight,
            Dock = DockStyle.Top,
            BackColor = btnNormal
        };
        titleBar.MouseDown += DragForm;
        Controls.Add(titleBar);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        var icon = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            Size = new Size(20, 20),
            Location = new Point(8, 8)
        };

        if (File.Exists(iconPath))
        {
            icon.Image = Image.FromFile(iconPath);
        }

        icon.MouseDown += DragForm;
        titleBar.Controls.Add(icon);

        var title = new Label
        {
            Text = $"Multi WebView - {profiles.Count} profiles",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(36, 9)
        };
        title.MouseDown += DragForm;
        titleBar.Controls.Add(title);

        titleBar.Controls.Add(CreateTitleButton("—", () => WindowState = FormWindowState.Minimized));
        btnMax = CreateTitleButton("⬜", ToggleMaximize);
        titleBar.Controls.Add(btnMax);
        titleBar.Controls.Add(CreateTitleButton("✕", Close, true));
    }

    private static void SetFormIcon(Form form)
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "icon.ico");
        if (File.Exists(iconPath))
        {
            form.Icon = new Icon(iconPath);
        }
    }

    private Button CreateTitleButton(string text, Action onClick, bool isClose = false)
    {
        var btn = new Button
        {
            Text = text,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(40, 36),
            Dock = DockStyle.Right,
            BackColor = btnNormal
        };

        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (_, _) => onClick();
        btn.MouseEnter += (_, _) => btn.BackColor = isClose ? btnCloseHover : btnHover;
        btn.MouseLeave += (_, _) => btn.BackColor = btnNormal;

        return btn;
    }

    private void BuildGrid()
    {
        var count = Math.Max(1, profiles.Count);
        var columns = (int)Math.Ceiling(Math.Sqrt(count));
        var rows = (int)Math.Ceiling(count / (double)columns);

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, TitleBarHeight, 0, 0),
            BackColor = Color.Black
        };
        Controls.Add(contentPanel);
        contentPanel.SendToBack();

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = rows,
            ColumnCount = columns,
            Padding = new Padding(4),
            BackColor = Color.Black
        };

        for (var row = 0; row < rows; row++)
        {
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));
        }

        for (var column = 0; column < columns; column++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columns));
        }

        contentPanel.Controls.Add(grid);

        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            var tile = CreateTile(profile, out var webView);
            webViews.Add(webView);
            grid.Controls.Add(tile, index % columns, index / columns);
        }
    }

    private Control CreateTile(Profile profile, out WebView2 webView)
    {
        var tile = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = new Padding(2),
            BackColor = Color.FromArgb(18, 18, 18)
        };
        tile.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new Label
        {
            Text = profile.Name,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(28, 28, 28),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        tile.Controls.Add(header, 0, 0);

        webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        tile.Controls.Add(webView, 0, 1);

        return tile;
    }

    private async Task InitializeWebViewsAsync()
    {
        for (var index = 0; index < webViews.Count; index++)
        {
            var webView = webViews[index];
            var profile = profiles[index];
            var userDataFolder = profileStore.GetWebViewUserDataFolder(profile);
            var environment = await WebViewEnvironmentFactory.CreateAsync(userDataFolder);

            await webView.EnsureCoreWebView2Async(environment);
            webView.Source = new Uri(profile.StartUrl);
        }
    }

    private void DragForm(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ReleaseCapture();
        SendMessage(Handle, WmNcButtonDown, HtCaption, 0);
    }

    private void ToggleMaximize()
    {
        if (isMaximized)
        {
            Bounds = previousBounds;
            isMaximized = false;
        }
        else
        {
            previousBounds = Bounds;
            Bounds = Screen.FromHandle(Handle).WorkingArea;
            isMaximized = true;
        }

        if (btnMax is not null)
        {
            btnMax.Text = isMaximized ? "❐" : "⬜";
        }
    }
}

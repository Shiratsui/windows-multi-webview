using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.WinForms;

namespace MultiWebView;

public sealed class BrowserForm : Form
{
    private readonly Profile profile;
    private readonly ProfileStore profileStore;
    private readonly string? launchUrl;
    private readonly Color btnNormal = Color.FromArgb(28, 28, 28);
    private readonly Color btnHover = Color.FromArgb(60, 60, 60);
    private readonly Color btnCloseHover = Color.FromArgb(232, 17, 35);
    private readonly Color btnActive = Color.FromArgb(25, 70, 115);
    private readonly int doubleClickThresholdMs = SystemInformation.DoubleClickTime;

    private WebView2 webView = null!;
    private Panel titleBar = null!;
    private Panel contentPanel = null!;
    private Button btnRefresh = null!;
    private Button btnPin = null!;
    private Button btnMin = null!;
    private Button btnMax = null!;
    private Button btnClose = null!;
    private Button btnMute = null!;
    private VolumeSliderControl volumeSlider = null!;
    private Label volumeValue = null!;
    private PictureBox dragIcon = null!;
    private Label titleLabel = null!;
    private DateTime lastClickTime = DateTime.MinValue;
    private Rectangle previousBounds;
    private bool isMaximized;
    private bool isPinned;
    private bool isMuted;
    private Screen currentScreen = null!;

    [DllImport("user32.dll")]
    private static extern void ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern void SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    private const int WmNcButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    public BrowserForm(Profile profile, ProfileStore profileStore, string? launchUrl = null)
    {
        this.profile = profile;
        this.profileStore = profileStore;
        this.launchUrl = launchUrl;
        isMuted = profile.IsMuted;

        Text = "Lara WebView";
        currentScreen = Screen.FromHandle(Handle);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        BackColor = Color.FromArgb(20, 20, 20);
        MinimumSize = new Size(800, 450);
        Size = new Size(1200, 800);
        SetFormIcon(this);

        Resize += (_, _) => UpdateMaxButtonIcon();
        Move += (_, _) => CheckMonitorChange();

        BuildTitleBar();
        BuildContent();
        BuildWebView();

        Shown += async (_, _) => await InitializeWebViewAsync();
    }

    private void BuildTitleBar()
    {
        titleBar = new Panel
        {
            Height = 36,
            Dock = DockStyle.Top,
            BackColor = btnNormal
        };

        titleBar.MouseDown += DragForm;
        Controls.Add(titleBar);

        dragIcon = CreateDragIcon();
        titleBar.Controls.Add(dragIcon);

        titleLabel = CreateTitleLabel();
        titleBar.Controls.Add(titleLabel);

        btnRefresh = CreateTitleButton("↻", () => RefreshWebView());
        titleBar.Controls.Add(btnRefresh);

        titleBar.Controls.Add(CreateVolumePanel());

        btnMin = CreateTitleButton("—", () => WindowState = FormWindowState.Minimized);
        titleBar.Controls.Add(btnMin);

        btnPin = CreateTitleButton("📌", TogglePin);
        titleBar.Controls.Add(btnPin);

        btnMax = CreateTitleButton("⬜", () => ToggleMaximize());
        titleBar.Controls.Add(btnMax);

        btnClose = CreateTitleButton("✕", () => Close(), true);
        titleBar.Controls.Add(btnClose);
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
        AddHoverEffect(btn, isClose);

        return btn;
    }

    private Panel CreateVolumePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Right,
            Width = 178,
            BackColor = btnNormal,
            Padding = new Padding(4, 6, 8, 6)
        };

        volumeSlider = new VolumeSliderControl
        {
            Dock = DockStyle.Right,
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(profile.VolumePercent, 0, 100),
            Width = 92,
            Height = 24
        };
        volumeSlider.ValueChanged += (_, _) =>
        {
            volumeValue.Text = $"{volumeSlider.Value}%";
            profileStore.UpdateProfileAudio(profile, volumeSlider.Value, isMuted);
            _ = WebViewVolumeController.ApplyAsync(webView, volumeSlider.Value, isMuted, profile.Name);
        };
        panel.Controls.Add(volumeSlider);

        btnMute = new Button
        {
            Text = isMuted ? "🔇" : "🔊",
            Dock = DockStyle.Right,
            ForeColor = Color.White,
            BackColor = isMuted ? btnActive : Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Width = 32
        };
        btnMute.FlatAppearance.BorderSize = 0;
        btnMute.Click += (_, _) =>
        {
            isMuted = !isMuted;
            btnMute.Text = isMuted ? "🔇" : "🔊";
            btnMute.BackColor = isMuted ? btnActive : Color.FromArgb(38, 38, 38);
            profileStore.UpdateProfileAudio(profile, volumeSlider.Value, isMuted);
            _ = WebViewVolumeController.ApplyAsync(webView, volumeSlider.Value, isMuted, profile.Name);
        };
        panel.Controls.Add(btnMute);

        volumeValue = new Label
        {
            Text = $"{volumeSlider.Value}%",
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = btnNormal,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular)
        };
        panel.Controls.Add(volumeValue);

        return panel;
    }

    private PictureBox CreateDragIcon()
    {
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
        return icon;
    }

    private Label CreateTitleLabel()
    {
        var label = new Label
        {
            Text = "Lara WebView",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(36, 9)
        };

        label.MouseDown += DragForm;
        return label;
    }

    private void AddHoverEffect(Button btn, bool isClose = false)
    {
        btn.BackColor = btnNormal;

        btn.MouseEnter += (_, _) =>
        {
            btn.BackColor = isClose ? btnCloseHover : btnHover;
        };

        btn.MouseLeave += (_, _) =>
        {
            btn.BackColor = btn == btnPin && isPinned ? btnActive : btnNormal;
        };
    }

    private async void RefreshWebView()
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        btnRefresh.Enabled = false;
        webView.CoreWebView2.Reload();
        await Task.Delay(800);
        btnRefresh.Enabled = true;
    }

    private void BuildContent()
    {
        contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 40, 0, 0)
        };

        Controls.Add(contentPanel);
    }

    private void BuildWebView()
    {
        webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        WebViewVolumeController.Attach(webView, () => volumeSlider.Value, () => isMuted, () => profile.Name);
        contentPanel.Controls.Add(webView);
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = profileStore.GetWebViewUserDataFolder(profile);
        var environment = await WebViewEnvironmentFactory.CreateAsync(userDataFolder);

        await webView.EnsureCoreWebView2Async(environment);
        await WebViewVolumeController.EnsureAudioSessionAsync(webView);
        await WebViewVolumeController.ConfigureAsync(webView, () => volumeSlider.Value, () => isMuted, () => profile.Name);
        webView.Source = new Uri(launchUrl ?? profile.StartUrl);

        ToggleMaximize();
    }

    private void DragForm(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        var now = DateTime.Now;
        if ((now - lastClickTime).TotalMilliseconds < doubleClickThresholdMs)
        {
            ToggleMaximize();
            lastClickTime = DateTime.MinValue;
            return;
        }

        lastClickTime = now;

        if (isMaximized)
        {
            var mouseScreen = Cursor.Position;
            WindowState = FormWindowState.Normal;

            var newX = mouseScreen.X - previousBounds.Width / 2;
            var newY = mouseScreen.Y - 15;

            Bounds = new Rectangle(newX, newY, previousBounds.Width, previousBounds.Height);
            isMaximized = false;
            UpdateMaxButtonIcon();
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
            var screen = Screen.FromHandle(Handle);
            Bounds = screen.WorkingArea;
            isMaximized = true;
        }

        UpdateMaxButtonIcon();
    }

    private void TogglePin()
    {
        isPinned = !isPinned;
        TopMost = isPinned;
        btnPin.BackColor = isPinned ? btnActive : btnNormal;
        btnPin.Text = isPinned ? "📍" : "📌";
    }

    private void CheckMonitorChange()
    {
        var newScreen = Screen.FromHandle(Handle);
        if (currentScreen == newScreen)
        {
            return;
        }

        var relativeX = previousBounds.X - currentScreen.WorkingArea.X;
        var relativeY = previousBounds.Y - currentScreen.WorkingArea.Y;

        previousBounds = new Rectangle(
            newScreen.WorkingArea.X + relativeX,
            newScreen.WorkingArea.Y + relativeY,
            previousBounds.Width,
            previousBounds.Height);

        currentScreen = newScreen;
    }

    private void UpdateMaxButtonIcon()
    {
        if (btnMax is not null)
        {
            btnMax.Text = isMaximized ? "❐" : "⬜";
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        BeginInvoke(new Action(() =>
        {
            Invalidate(true);
            Update();
        }));
    }
}

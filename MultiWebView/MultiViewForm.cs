using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MultiWebView;

public sealed class MultiViewForm : Form
{
    private const int TitleBarHeight = 36;

    private readonly IReadOnlyList<Profile> profiles;
    private readonly ProfileStore profileStore;
    private readonly List<WebView2> webViews = [];
    private readonly Dictionary<WebView2, int> volumeByWebView = [];
    private readonly Dictionary<WebView2, bool> mutedByWebView = [];
    private readonly Dictionary<WebView2, StatsOverlayState> statsByWebView = [];
    private readonly List<ContextMenuStrip> statsMenus = [];
    private readonly ToolTip toolTip = new();
    private readonly NotifyIcon trayIcon = new();
    private readonly Color btnNormal = Color.FromArgb(28, 28, 28);
    private readonly Color btnHover = Color.FromArgb(60, 60, 60);
    private readonly Color btnCloseHover = Color.FromArgb(232, 17, 35);
    private readonly Color btnActive = Color.FromArgb(25, 70, 115);
    private Button btnMin = null!;
    private Button btnTray = null!;
    private Button btnPin = null!;
    private Button btnMax = null!;
    private Point? pendingTitleBarDragStart;
    private Rectangle previousBounds;
    private bool isMaximized;
    private bool isPinned;
    private bool isMinimizedToTray;

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

        Text = WindowIdentity.BuildMultiViewTitle(profiles);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Normal;
        BackColor = Color.FromArgb(20, 20, 20);
        MinimumSize = new Size(1000, 650);
        Size = new Size(1400, 900);
        SetMultiViewIcon(this, profiles);

        ConfigureTrayIcon();
        BuildTitleBar();
        BuildGrid();

        Shown += async (_, _) =>
        {
            ToggleMaximize();
            await InitializeWebViewsAsync();
        };
        Deactivate += (_, _) => CloseStatsMenus();
    }

    private void ConfigureTrayIcon()
    {
        trayIcon.Text = WindowIdentity.BuildTrayText(Text);
        trayIcon.Visible = false;
        trayIcon.Icon = WindowIdentity.CreateMultiViewIcon(profiles);

        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(6),
            ShowImageMargin = false,
            Renderer = new DarkTrayMenuRenderer()
        };

        var restoreItem = new ToolStripMenuItem("Restore")
        {
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Height = 32,
            Width = 156,
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        restoreItem.Click += (_, _) => RestoreFromTray();

        var closeItem = new ToolStripMenuItem("Close")
        {
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Height = 32,
            Width = 156,
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        closeItem.Click += (_, _) => Close();

        menu.Items.Add(restoreItem);
        menu.Items.Add(closeItem);
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Height = TitleBarHeight,
            Dock = DockStyle.Top,
            BackColor = btnNormal
        };
        AttachTitleBarDrag(titleBar);
        Controls.Add(titleBar);

        var icon = new PictureBox
        {
            SizeMode = PictureBoxSizeMode.StretchImage,
            Image = Icon!.ToBitmap(),
            Size = new Size(20, 20),
            Location = new Point(8, 8)
        };

        AttachTitleBarDrag(icon);
        titleBar.Controls.Add(icon);

        var title = new Label
        {
            Text = Text,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(36, 0),
            Size = new Size(Math.Max(180, Width - 240), TitleBarHeight),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        toolTip.SetToolTip(title, Text);
        AttachTitleBarDrag(title);
        titleBar.Controls.Add(title);

        btnMin = CreateTitleButton("—", () => WindowState = FormWindowState.Minimized);
        titleBar.Controls.Add(btnMin);
        btnTray = CreateTitleButton("▾", MinimizeToTray);
        titleBar.Controls.Add(btnTray);
        btnPin = CreateTitleButton("📌", TogglePin);
        titleBar.Controls.Add(btnPin);
        btnMax = CreateTitleButton("⬜", ToggleMaximize);
        titleBar.Controls.Add(btnMax);
        titleBar.Controls.Add(CreateTitleButton("✕", Close, true));
    }

    private static void SetMultiViewIcon(Form form, IReadOnlyList<Profile> profiles)
    {
        form.Icon = WindowIdentity.CreateMultiViewIcon(profiles);
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
        btn.MouseLeave += (_, _) => btn.BackColor = btn == btnPin && isPinned ? btnActive : btnNormal;

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
        tile.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        tile.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            BackColor = Color.FromArgb(28, 28, 28),
            Padding = new Padding(8, 0, 6, 0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

        var nameLabel = new Label
        {
            Text = profile.Name,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(28, 28, 28),
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        header.Controls.Add(nameLabel, 0, 0);

        var refreshButton = new Button
        {
            Text = "⭮",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold),
            Margin = new Padding(4, 0, 2, 0)
        };
        refreshButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(refreshButton, "Refresh");
        header.Controls.Add(refreshButton, 1, 0);

        var screenshotButton = new Button
        {
            Text = "📷",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 9F, FontStyle.Regular),
            Margin = new Padding(2, 0, 2, 0)
        };
        screenshotButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(screenshotButton, "Save screenshot to profile folder");
        header.Controls.Add(screenshotButton, 2, 0);

        var folderButton = new Button
        {
            Text = "📁",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Emoji", 9F, FontStyle.Regular),
            Margin = new Padding(2, 0, 2, 0)
        };
        folderButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(folderButton, "Show profile folder");
        header.Controls.Add(folderButton, 3, 0);

        var fpsButton = new Button
        {
            Text = "STAT",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            Margin = new Padding(2, 0, 2, 0)
        };
        fpsButton.FlatAppearance.BorderSize = 0;
        toolTip.SetToolTip(fpsButton, "Show stats overlay");
        header.Controls.Add(fpsButton, 4, 0);

        var volumeValue = new Label
        {
            Text = $"{Math.Clamp(profile.VolumePercent, 0, 100)}%",
            Dock = DockStyle.Fill,
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(28, 28, 28),
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular)
        };
        header.Controls.Add(volumeValue, 5, 0);

        var muted = profile.IsMuted;
        var muteButton = new Button
        {
            Text = muted ? "🔇" : "🔊",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = muted ? btnActive : Color.FromArgb(38, 38, 38),
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(4, 0, 2, 0)
        };
        muteButton.FlatAppearance.BorderSize = 0;
        header.Controls.Add(muteButton, 6, 0);

        var volumeSlider = new VolumeSliderControl
        {
            Dock = DockStyle.Fill,
            Minimum = 0,
            Maximum = 100,
            Value = Math.Clamp(profile.VolumePercent, 0, 100),
            Height = 24,
            Margin = new Padding(4, 3, 0, 0)
        };
        header.Controls.Add(volumeSlider, 7, 0);

        tile.Controls.Add(header, 0, 0);

        webView = new WebView2
        {
            Dock = DockStyle.Fill
        };
        var tileWebView = webView;
        volumeByWebView[tileWebView] = volumeSlider.Value;
        mutedByWebView[tileWebView] = muted;
        statsByWebView[tileWebView] = new StatsOverlayState
        {
            ShowFps = profile.ShowStatsFps,
            ShowCpu = profile.ShowStatsCpu,
            ShowMemory = profile.ShowStatsMemory,
            IsHorizontal = profile.ShowStatsHorizontal
        };
        WebViewVolumeController.Attach(
            tileWebView,
            () => volumeByWebView.GetValueOrDefault(tileWebView, 100),
            () => isMinimizedToTray || mutedByWebView.GetValueOrDefault(tileWebView),
            () => profile.Name);

        refreshButton.Click += (_, _) =>
        {
            tileWebView.CoreWebView2?.Reload();
        };

        screenshotButton.Click += async (_, _) =>
        {
            await SaveScreenshotAsync(tileWebView, profile);
        };

        folderButton.Click += (_, _) =>
        {
            OpenProfileFolder(profile);
        };

        ConfigureStatsButton(fpsButton, tileWebView, profile);

        volumeSlider.ValueChanged += (_, _) =>
        {
            volumeValue.Text = $"{volumeSlider.Value}%";
            volumeByWebView[tileWebView] = volumeSlider.Value;
            profileStore.UpdateProfileAudio(profile, volumeSlider.Value, muted);
            _ = WebViewVolumeController.ApplyAsync(
                tileWebView,
                volumeSlider.Value,
                isMinimizedToTray || muted,
                profile.Name);
        };

        muteButton.Click += (_, _) =>
        {
            muted = !muted;
            mutedByWebView[tileWebView] = muted;
            muteButton.Text = muted ? "🔇" : "🔊";
            muteButton.BackColor = muted ? btnActive : Color.FromArgb(38, 38, 38);
            profileStore.UpdateProfileAudio(profile, volumeSlider.Value, muted);
            _ = WebViewVolumeController.ApplyAsync(
                tileWebView,
                volumeSlider.Value,
                isMinimizedToTray || muted,
                profile.Name);
        };

        tile.Controls.Add(tileWebView, 0, 1);

        return tile;
    }

    private void ConfigureStatsButton(Button statsButton, WebView2 webView, Profile profile)
    {
        var menu = new ContextMenuStrip
        {
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F),
            ShowImageMargin = false,
            Renderer = new StatsMenuRenderer(),
            AutoClose = false
        };
        var menuFilter = new StatsMenuMessageFilter(menu, statsButton);
        var menuFilterAttached = false;
        statsMenus.Add(menu);

        var state = statsByWebView[webView];
        var fpsItem = CreateStatsMenuItem("FPS", state.ShowFps, (_, checkedValue) => statsByWebView[webView].ShowFps = checkedValue);
        var cpuItem = CreateStatsMenuItem("CPU", state.ShowCpu, (_, checkedValue) => statsByWebView[webView].ShowCpu = checkedValue);
        var memoryItem = CreateStatsMenuItem("Memory", state.ShowMemory, (_, checkedValue) => statsByWebView[webView].ShowMemory = checkedValue);
        var horizontalItem = CreateStatsMenuItem("Horizontal", state.IsHorizontal, (_, checkedValue) => statsByWebView[webView].IsHorizontal = checkedValue);
        statsButton.BackColor = state.AnyEnabled ? btnActive : Color.FromArgb(38, 38, 38);

        menu.Items.AddRange([fpsItem, cpuItem, memoryItem, horizontalItem]);

        statsButton.Click += (_, _) =>
        {
            if (menu.Visible)
            {
                menu.Close();
                return;
            }

            if (!menuFilterAttached)
            {
                Application.AddMessageFilter(menuFilter);
                menuFilterAttached = true;
            }

            menu.Show(statsButton, new Point(0, statsButton.Height));
        };

        menu.Closed += (_, _) =>
        {
            if (!menuFilterAttached)
            {
                return;
            }

            Application.RemoveMessageFilter(menuFilter);
            menuFilterAttached = false;
        };

        ToolStripMenuItem CreateStatsMenuItem(string text, bool isChecked, Action<WebView2, bool> update)
        {
            var item = new ToolStripMenuItem(text)
            {
                AutoSize = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 148,
                Height = 30,
                Padding = Padding.Empty,
                Tag = isChecked
            };
            item.Click += async (_, _) =>
            {
                var checkedValue = item.Tag is not true;
                item.Tag = checkedValue;
                update(webView, checkedValue);
                var updatedState = statsByWebView[webView];
                statsButton.BackColor = updatedState.AnyEnabled ? btnActive : Color.FromArgb(38, 38, 38);
                profileStore.UpdateProfileStats(
                    profile,
                    updatedState.ShowFps,
                    updatedState.ShowCpu,
                    updatedState.ShowMemory,
                    updatedState.IsHorizontal);
                await SetNativeFpsCounterAsync(webView, updatedState.ShowFps);
                await RefreshStatsOverlayAsync(webView);
                EnsureStatsTimer(webView);
            };

            return item;
        }
    }

    private void EnsureStatsTimer(WebView2 webView)
    {
        var state = statsByWebView[webView];

        if (!state.AnyEnabled)
        {
            state.Timer?.Stop();
            state.Timer?.Dispose();
            state.Timer = null;
            state.ResetSample();
            return;
        }

        if (state.Timer is not null)
        {
            return;
        }

        state.ResetSample();
        state.Timer = new System.Windows.Forms.Timer { Interval = 1000 };
        state.Timer.Tick += async (_, _) => await RefreshStatsOverlayAsync(webView);
        state.Timer.Start();
    }

    private async Task RefreshStatsOverlayAsync(WebView2 webView)
    {
        if (!statsByWebView.TryGetValue(webView, out var state) || webView.CoreWebView2 is null || webView.IsDisposed)
        {
            return;
        }

        if (!state.AnyEnabled)
        {
            await ExecuteStatsScriptAsync(webView, CreateStatsOverlayScript(null));
            return;
        }

        var snapshot = await CreateStatsSnapshotAsync(webView, state);
        await ExecuteStatsScriptAsync(webView, CreateStatsOverlayScript(snapshot));
    }

    private static async Task SetNativeFpsCounterAsync(WebView2 webView, bool show)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var parameters = show ? "{\"show\":true}" : "{\"show\":false}";
            await webView.CoreWebView2.CallDevToolsProtocolMethodAsync(
                "Rendering.setShowFPSCounter",
                parameters);
        }
        catch
        {
            // The FPS overlay is a Chromium debugging feature; unsupported runtimes should not break browsing.
        }
    }

    private static async Task ExecuteStatsScriptAsync(WebView2 webView, string script)
    {
        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch
        {
            // Page-script injection can fail during navigation; the next navigation completion will retry if enabled.
        }
    }

    private Task<object> CreateStatsSnapshotAsync(WebView2 webView, StatsOverlayState state)
    {
        var cpuText = "--";
        var memoryText = "--";

        if (state.ShowCpu || state.ShowMemory)
        {
            SampleWebViewProcessTree(webView, state, out cpuText, out memoryText);
        }

        return Task.FromResult<object>(new
        {
            fps = state.ShowFps,
            cpu = state.ShowCpu ? cpuText : null,
            memory = state.ShowMemory ? memoryText : null,
            horizontal = state.IsHorizontal
        });
    }

    private static void SampleWebViewProcessTree(WebView2 webView, StatsOverlayState state, out string cpuText, out string memoryText)
    {
        cpuText = "--";
        memoryText = "--";

        try
        {
            var processIds = WebViewVolumeController.GetProcessTreeIds((int)webView.CoreWebView2.BrowserProcessId);
            var totalProcessorTime = TimeSpan.Zero;
            var totalMemoryBytes = 0L;

            foreach (var processId in processIds)
            {
                try
                {
                    using var process = Process.GetProcessById(processId);
                    totalProcessorTime += process.TotalProcessorTime;
                    totalMemoryBytes += process.WorkingSet64;
                }
                catch
                {
                }
            }

            var now = DateTime.UtcNow;
            if (state.LastSampleUtc is not null)
            {
                var elapsedSeconds = Math.Max(0.001, (now - state.LastSampleUtc.Value).TotalSeconds);
                var cpuSeconds = Math.Max(0, (totalProcessorTime - state.LastProcessorTime).TotalSeconds);
                var cpuPercent = Math.Clamp(cpuSeconds / elapsedSeconds / Environment.ProcessorCount * 100, 0, 999);
                cpuText = $"{cpuPercent:0}%";
            }

            memoryText = $"{totalMemoryBytes / 1024d / 1024d:0} MB";
            state.LastProcessorTime = totalProcessorTime;
            state.LastSampleUtc = now;
        }
        catch
        {
        }
    }

    private static string CreateStatsOverlayScript(object? snapshot)
    {
        if (snapshot is null)
        {
            return """
                (() => {
                    window.__multiWebViewStatsEnabled = false;
                    if (window.__multiWebViewStatsFrame) {
                        cancelAnimationFrame(window.__multiWebViewStatsFrame);
                        window.__multiWebViewStatsFrame = 0;
                    }
                    document.getElementById("__multi_webview_stats_overlay")?.remove();
                })();
                """;
        }

        var json = JsonSerializer.Serialize(snapshot);
        return """
            (() => {
                window.__multiWebViewStatsEnabled = true;
                window.__multiWebViewStats = __SNAPSHOT__;

                const existing = document.getElementById("__multi_webview_stats_overlay");

                const overlay = existing || document.createElement("div");
                overlay.id = "__multi_webview_stats_overlay";
                overlay.style.cssText = [
                    "position:fixed",
                    "left:10px",
                    "top:10px",
                    "z-index:2147483647",
                    "min-width:86px",
                    "padding:6px 8px",
                    "border:1px solid rgba(120,255,120,.45)",
                    "border-radius:4px",
                    "background:rgba(0,0,0,.72)",
                    "color:#39ff5a",
                    "font:700 13px Consolas, monospace",
                    "line-height:1.35",
                    "text-shadow:0 1px 2px #000",
                    "pointer-events:none"
                ].join(";");
                if (!existing) {
                    (document.body || document.documentElement).appendChild(overlay);
                }

                window.__multiWebViewStatsFrames ??= 0;
                window.__multiWebViewStatsLast ??= performance.now();
                window.__multiWebViewStatsFrameStart ??= window.__multiWebViewStatsLast;
                window.__multiWebViewStatsFps ??= "--";
                window.__multiWebViewStatsFrameMs ??= "--";

                const render = () => {
                    const data = window.__multiWebViewStats || {};
                    const lines = [];
                    if (data.fps) {
                        lines.push(`<span style="color:#39ff5a">FPS</span> ${window.__multiWebViewStatsFps}`);
                        lines.push(`<span style="color:#ffffff">LAT</span> ${window.__multiWebViewStatsFrameMs} ms`);
                    }
                    if (data.cpu !== null && data.cpu !== undefined) {
                        lines.push(`<span style="color:#7fc7ff">CPU</span> ${data.cpu}`);
                    }
                    if (data.memory !== null && data.memory !== undefined) {
                        lines.push(`<span style="color:#ffcf5a">MEM</span> ${data.memory}`);
                    }
                    overlay.innerHTML = data.horizontal ? lines.join(" <span style=\"color:#777\">|</span> ") : lines.join("<br>");
                };

                const tick = now => {
                    if (!window.__multiWebViewStatsEnabled) {
                        overlay.remove();
                        window.__multiWebViewStatsFrame = 0;
                        return;
                    }

                    window.__multiWebViewStatsFrames++;
                    window.__multiWebViewStatsFrameMs = (now - window.__multiWebViewStatsFrameStart).toFixed(1);
                    window.__multiWebViewStatsFrameStart = now;

                    if (now - window.__multiWebViewStatsLast >= 500) {
                        window.__multiWebViewStatsFps = Math.round(
                            window.__multiWebViewStatsFrames * 1000 / (now - window.__multiWebViewStatsLast));
                        window.__multiWebViewStatsFrames = 0;
                        window.__multiWebViewStatsLast = now;
                        render();
                    }

                    window.__multiWebViewStatsFrame = requestAnimationFrame(tick);
                };

                render();
                if (!window.__multiWebViewStatsFrame) {
                    window.__multiWebViewStatsFrame = requestAnimationFrame(tick);
                }
            })();
            """.Replace("__SNAPSHOT__", json);
    }

    private async Task SaveScreenshotAsync(WebView2 webView, Profile profile)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            var screenshotsFolder = GetScreenshotsFolder(profile);
            Directory.CreateDirectory(screenshotsFolder);

            var screenshotPath = Path.Combine(screenshotsFolder, CreateScreenshotFileName(profile));
            await using var stream = File.Create(screenshotPath);
            await webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, stream);
            ShowTemporaryStatus(webView, profile, "Screenshot Taken", $"Saved on {screenshotPath}");
        }
        catch
        {
            // Screenshot capture is intentionally silent from the UI.
        }
    }

    private void OpenProfileFolder(Profile profile)
    {
        var profileFolder = profileStore.GetProfileFolder(profile);
        Directory.CreateDirectory(profileFolder);
        Directory.CreateDirectory(GetScreenshotsFolder(profile));

        Process.Start(new ProcessStartInfo
        {
            FileName = profileFolder,
            UseShellExecute = true
        });
    }

    private void OpenScreenshotsFolder(Profile profile)
    {
        var screenshotsFolder = GetScreenshotsFolder(profile);
        Directory.CreateDirectory(screenshotsFolder);

        Process.Start(new ProcessStartInfo
        {
            FileName = screenshotsFolder,
            UseShellExecute = true
        });
    }

    private string GetScreenshotsFolder(Profile profile)
    {
        return Path.Combine(profileStore.GetProfileFolder(profile), "screenshots");
    }

    private static string CreateScreenshotFileName(Profile profile)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedName = new string(profile.Name
            .Select(character => invalidChars.Contains(character) ? '_' : character)
            .ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitizedName))
        {
            sanitizedName = "WebView";
        }

        return $"{sanitizedName}-{DateTime.Now:yyyyMMdd-HHmmss}.png";
    }

    private void ShowTemporaryStatus(Control target, Profile profile, string title, string detail)
    {
        const int toastWidth = 430;
        const int toastHeight = 92;
        const int toastMargin = 16;
        var toastColor = Color.FromArgb(20, 135, 78);

        var targetScreenBounds = new Rectangle(target.PointToScreen(Point.Empty), target.ClientSize);
        var finalLocation = new Point(
            targetScreenBounds.Right - toastWidth - toastMargin,
            targetScreenBounds.Top + toastMargin);

        var popup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            ShowInTaskbar = false,
            TopMost = true,
            BackColor = toastColor,
            Size = new Size(toastWidth, toastHeight),
            Location = new Point(finalLocation.X, finalLocation.Y - toastHeight - 8),
            Opacity = 1
        };

        SetMultiViewIcon(popup, profiles);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = toastColor,
            Padding = new Padding(14, 8, 14, 8)
        };
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            BackColor = toastColor,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var detailLabel = new Label
        {
            Text = detail,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(225, 255, 238),
            BackColor = toastColor,
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        var footerLabel = new Label
        {
            Text = "Click to view folder",
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(205, 255, 226),
            BackColor = toastColor,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        void AttachOpenFolderClick(Control control)
        {
            control.Cursor = Cursors.Hand;
            control.Click += (_, _) =>
            {
                if (!popup.IsDisposed)
                {
                    popup.Close();
                    popup.Dispose();
                }

                OpenScreenshotsFolder(profile);
            };
        }

        content.Controls.Add(titleLabel, 0, 0);
        content.Controls.Add(detailLabel, 0, 1);
        content.Controls.Add(footerLabel, 0, 2);
        popup.Controls.Add(content);
        AttachOpenFolderClick(popup);
        AttachOpenFolderClick(content);
        AttachOpenFolderClick(titleLabel);
        AttachOpenFolderClick(detailLabel);
        AttachOpenFolderClick(footerLabel);

        var slideTimer = new System.Windows.Forms.Timer { Interval = 15 };
        slideTimer.Tick += (_, _) =>
        {
            if (popup.IsDisposed)
            {
                slideTimer.Stop();
                slideTimer.Dispose();
                return;
            }

            var nextY = Math.Min(finalLocation.Y, popup.Top + 10);
            popup.Location = new Point(finalLocation.X, nextY);

            if (nextY == finalLocation.Y)
            {
                slideTimer.Stop();
            }
        };

        var closeTimer = new System.Windows.Forms.Timer { Interval = 1500 };
        var fadeTimer = new System.Windows.Forms.Timer { Interval = 30 };
        fadeTimer.Tick += (_, _) =>
        {
            if (popup.IsDisposed)
            {
                fadeTimer.Stop();
                fadeTimer.Dispose();
                return;
            }

            popup.Opacity -= 0.08;
            if (popup.Opacity > 0.05)
            {
                return;
            }

            fadeTimer.Stop();
            closeTimer.Stop();
            slideTimer.Stop();
            fadeTimer.Dispose();
            closeTimer.Dispose();
            slideTimer.Dispose();
            popup.Close();
            popup.Dispose();
        };

        closeTimer.Tick += (_, _) =>
        {
            closeTimer.Stop();
            slideTimer.Stop();
            fadeTimer.Start();
        };

        popup.Shown += (_, _) =>
        {
            slideTimer.Start();
            closeTimer.Start();
        };
        popup.Show(this);
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
            webView.CoreWebView2.WebMessageReceived += (_, args) =>
            {
                if (args.TryGetWebMessageAsString() == "__multi_webview_close_stats_menu")
                {
                    CloseStatsMenus();
                }
            };
            webView.CoreWebView2.NavigationCompleted += async (_, _) =>
            {
                if (statsByWebView.GetValueOrDefault(webView)?.AnyEnabled == true)
                {
                    await RefreshStatsOverlayAsync(webView);
                }

                await InstallStatsMenuCloseHandlersAsync(webView);
            };
            webView.CoreWebView2.ContainsFullScreenElementChanged += (_, _) => CloseStatsMenus();
            await WebViewVolumeController.EnsureAudioSessionAsync(webView);
            await WebViewVolumeController.ConfigureAsync(
                webView,
                () => volumeByWebView.GetValueOrDefault(webView, 100),
                () => isMinimizedToTray || mutedByWebView.GetValueOrDefault(webView),
                () => profile.Name);
            await SetNativeFpsCounterAsync(webView, statsByWebView.GetValueOrDefault(webView)?.ShowFps == true);
            await RefreshStatsOverlayAsync(webView);
            EnsureStatsTimer(webView);
            webView.Source = new Uri(profile.StartUrl);
        }
    }

    private void AttachTitleBarDrag(Control control)
    {
        control.MouseDown += TitleBarMouseDown;
        control.MouseMove += TitleBarMouseMove;
        control.MouseUp += TitleBarMouseUp;
        control.MouseDoubleClick += TitleBarMouseDoubleClick;
    }

    private void TitleBarMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        pendingTitleBarDragStart = Cursor.Position;
    }

    private void TitleBarMouseMove(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || pendingTitleBarDragStart is not { } dragStart)
        {
            return;
        }

        var dragSize = SystemInformation.DragSize;
        var dragBounds = new Rectangle(
            dragStart.X - dragSize.Width / 2,
            dragStart.Y - dragSize.Height / 2,
            dragSize.Width,
            dragSize.Height);

        if (dragBounds.Contains(Cursor.Position))
        {
            return;
        }

        pendingTitleBarDragStart = null;
        DragForm();
    }

    private void TitleBarMouseUp(object? sender, MouseEventArgs e)
    {
        pendingTitleBarDragStart = null;
    }

    private void TitleBarMouseDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        pendingTitleBarDragStart = null;
        ToggleMaximize();
    }

    private void DragForm()
    {
        if (isMaximized)
        {
            var mouseScreen = Cursor.Position;
            var width = previousBounds.Width;
            var height = previousBounds.Height;
            var newX = mouseScreen.X - width / 2;
            var newY = mouseScreen.Y - 15;

            Bounds = new Rectangle(newX, newY, width, height);
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
            Bounds = Screen.FromHandle(Handle).WorkingArea;
            isMaximized = true;
        }

        if (btnMax is not null)
        {
            UpdateMaxButtonIcon();
        }
    }

    private void UpdateMaxButtonIcon()
    {
        if (btnMax is not null)
        {
            btnMax.Text = isMaximized ? "❐" : "⬜";
        }
    }

    private void TogglePin()
    {
        isPinned = !isPinned;
        TopMost = isPinned;
        btnPin.BackColor = isPinned ? btnActive : btnNormal;
        btnPin.Text = isPinned ? "📍" : "📌";
    }

    private void MinimizeToTray()
    {
        if (isMinimizedToTray)
        {
            return;
        }

        pendingTitleBarDragStart = null;
        ResetTitleButtonColors();
        isMinimizedToTray = true;
        trayIcon.Visible = true;
        _ = ApplyTrayMuteStateAsync(true);
        Hide();
        ShowInTaskbar = false;
    }

    private void RestoreFromTray()
    {
        if (!isMinimizedToTray)
        {
            return;
        }

        isMinimizedToTray = false;
        ShowInTaskbar = true;
        Show();
        _ = ApplyTrayMuteStateAsync(false);
        ResetTitleButtonColors();
        trayIcon.Visible = false;
        Activate();
        BringToFront();
    }

    public void ActivateFromProfilePicker()
    {
        if (isMinimizedToTray)
        {
            RestoreFromTray();
            return;
        }

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        ShowInTaskbar = true;
        Activate();
        BringToFront();
    }

    private async Task ApplyTrayMuteStateAsync(bool muted)
    {
        for (var index = 0; index < webViews.Count; index++)
        {
            var webView = webViews[index];
            var profile = profiles[index];
            var effectiveMuted = muted || mutedByWebView.GetValueOrDefault(webView);

            await WebViewVolumeController.ApplyAsync(
                webView,
                volumeByWebView.GetValueOrDefault(webView, 100),
                effectiveMuted,
                profile.Name);
        }
    }

    private void ResetTitleButtonColors()
    {
        btnMin.BackColor = btnNormal;
        btnTray.BackColor = btnNormal;
        btnPin.BackColor = isPinned ? btnActive : btnNormal;
        btnMax.BackColor = btnNormal;
    }

    private void CloseStatsMenus()
    {
        foreach (var menu in statsMenus)
        {
            if (menu.Visible)
            {
                menu.Close();
            }
        }
    }

    private async Task InstallStatsMenuCloseHandlersAsync(WebView2 webView)
    {
        if (webView.CoreWebView2 is null)
        {
            return;
        }

        try
        {
            await webView.CoreWebView2.ExecuteScriptAsync("""
                (() => {
                    if (window.__multiWebViewStatsMenuCloseHooked) {
                        return;
                    }

                    window.__multiWebViewStatsMenuCloseHooked = true;
                    const closeMenu = () => chrome.webview.postMessage("__multi_webview_close_stats_menu");
                    window.addEventListener("pointerdown", closeMenu, true);
                    window.addEventListener("mousedown", closeMenu, true);
                    window.addEventListener("touchstart", closeMenu, true);
                    window.addEventListener("wheel", closeMenu, true);
                })();
                """);
        }
        catch
        {
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var state in statsByWebView.Values)
            {
                state.Timer?.Stop();
                state.Timer?.Dispose();
            }

            toolTip.Dispose();
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class StatsOverlayState
    {
        public bool ShowFps { get; set; }
        public bool ShowCpu { get; set; }
        public bool ShowMemory { get; set; }
        public bool IsHorizontal { get; set; }
        public System.Windows.Forms.Timer? Timer { get; set; }
        public DateTime? LastSampleUtc { get; set; }
        public TimeSpan LastProcessorTime { get; set; }
        public bool AnyEnabled => ShowFps || ShowCpu || ShowMemory;

        public void ResetSample()
        {
            LastSampleUtc = null;
            LastProcessorTime = TimeSpan.Zero;
        }
    }

    private sealed class StatsMenuRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color MenuBack = Color.FromArgb(18, 18, 18);
        private static readonly Color MenuBorder = Color.FromArgb(64, 64, 64);
        private static readonly Color ItemHover = Color.FromArgb(42, 42, 42);
        private static readonly Color TextColor = Color.White;

        public StatsMenuRenderer()
            : base(new StatsMenuColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(MenuBack);
            e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(MenuBorder);
            var bounds = new Rectangle(Point.Empty, e.ToolStrip.Size - new Size(1, 1));
            e.Graphics.DrawRectangle(pen, bounds);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            using var brush = new SolidBrush(e.Item.Selected ? ItemHover : MenuBack);
            e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
        }

        protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e)
        {
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            var checkBounds = new Rectangle(10, Math.Max(0, (e.Item.Height - 16) / 2), 16, 16);
            using var boxBrush = new SolidBrush(Color.FromArgb(18, 18, 18));
            using var boxBorderPen = new Pen(Color.FromArgb(95, 95, 95));
            e.Graphics.FillRectangle(boxBrush, checkBounds);
            e.Graphics.DrawRectangle(boxBorderPen, checkBounds);

            if (e.Item.Tag is true)
            {
                using var checkPen = new Pen(Color.FromArgb(57, 255, 90), 2.4F)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round
                };
                e.Graphics.DrawLines(
                    checkPen,
                    [
                        new Point(checkBounds.Left + 3, checkBounds.Top + 8),
                        new Point(checkBounds.Left + 7, checkBounds.Top + 12),
                        new Point(checkBounds.Left + 13, checkBounds.Top + 4)
                    ]);
            }

            var textBounds = new Rectangle(38, 0, Math.Max(0, e.Item.Width - 46), e.Item.Height);
            TextRenderer.DrawText(
                e.Graphics,
                e.Text,
                e.TextFont,
                textBounds,
                TextColor,
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.Left |
                TextFormatFlags.SingleLine |
                TextFormatFlags.NoPadding);
        }

        private sealed class StatsMenuColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground => MenuBack;
            public override Color MenuBorder => StatsMenuRenderer.MenuBorder;
            public override Color MenuItemSelected => ItemHover;
            public override Color MenuItemBorder => ItemHover;
            public override Color ImageMarginGradientBegin => MenuBack;
            public override Color ImageMarginGradientMiddle => MenuBack;
            public override Color ImageMarginGradientEnd => MenuBack;
        }
    }

    private sealed class StatsMenuMessageFilter : IMessageFilter
    {
        private const int WmLeftButtonDown = 0x0201;
        private const int WmRightButtonDown = 0x0204;
        private const int WmMiddleButtonDown = 0x0207;
        private const int WmNonClientLeftButtonDown = 0x00A1;

        private readonly ContextMenuStrip menu;
        private readonly Control ownerButton;

        public StatsMenuMessageFilter(ContextMenuStrip menu, Control ownerButton)
        {
            this.menu = menu;
            this.ownerButton = ownerButton;
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (!menu.Visible || !IsMouseDownMessage(m.Msg))
            {
                return false;
            }

            var cursorPosition = Cursor.Position;
            var buttonBounds = ownerButton.RectangleToScreen(ownerButton.ClientRectangle);

            if (!menu.Bounds.Contains(cursorPosition) && !buttonBounds.Contains(cursorPosition))
            {
                menu.Close();
            }

            return false;
        }

        private static bool IsMouseDownMessage(int message)
        {
            return message is WmLeftButtonDown or WmRightButtonDown or WmMiddleButtonDown or WmNonClientLeftButtonDown;
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MultiWebView;

public sealed class ProfilePickerForm : Form
{
    private const int WmNcButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    private readonly ProfileStore profileStore = new();
    private readonly HashSet<string> selectedProfileIds = [];
    private readonly HashSet<string> openProfileIds = [];
    private readonly Dictionary<string, Panel> profileCards = [];
    private readonly Dictionary<string, ProfileCardView> profileCardViews = [];
    private readonly Dictionary<string, MultiViewForm> openProfileWindows = [];
    private readonly Dictionary<MultiViewForm, HashSet<string>> openProfileIdsByWindow = [];
    private readonly List<Form> openWindows = [];
    private readonly FlowLayoutPanel profileList = new BufferedFlowLayoutPanel();
    private readonly TextBox profileNameTextBox = new();
    private readonly TextBox profileUrlTextBox = new();
    private readonly NotifyIcon trayIcon = new();
    private readonly System.Windows.Forms.Timer profileUsageTimer = new() { Interval = 1000 };
    private ActionButtonControl? createMultiViewButton;
    private Form? profileUsagePopup;
    private Label? usageTitleLabel;
    private Label? usageStateLabel;
    private Label? usageCpuValueLabel;
    private Label? usageMemoryValueLabel;
    private Label? usageGpuValueLabel;
    private Label? usageGpuMemoryValueLabel;
    private Profile? hoveredOpenProfile;
    private Panel? hoveredOpenProfileCard;
    private bool isUpdatingProfileUsage;
    private readonly Color btnNormal = Color.FromArgb(28, 28, 28);
    private readonly Color btnHover = Color.FromArgb(60, 60, 60);
    private readonly Color btnCloseHover = Color.FromArgb(232, 17, 35);
    private readonly Color btnActive = Color.FromArgb(25, 70, 115);
    private readonly Color profileCardNormal = Color.FromArgb(30, 30, 30);
    private readonly Color profileCardHover = Color.FromArgb(42, 42, 42);
    private readonly Color profileCardOpen = Color.FromArgb(28, 48, 40);
    private readonly Color profileCardSelected = Color.FromArgb(25, 70, 115);
    private Button? btnPin;
    private Button? btnMax;
    private Button? btnClose;
    private Point? pendingTitleBarDragStart;
    private Rectangle previousBounds;
    private FormWindowState windowStateBeforeTray = FormWindowState.Normal;
    private bool isPinned;
    private bool isMaximized;
    private bool isMinimizedToTray;

    [DllImport("user32.dll")]
    private static extern void ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern void SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    public ProfilePickerForm()
    {
        Text = "Multi WebView";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(760, 520);
        Size = new Size(900, 620);
        BackColor = Color.FromArgb(20, 20, 20);
        Font = new Font("Segoe UI", 10F);
        SetPickerIcon(this);

        ConfigureTrayIcon();
        BuildLayout();
        BuildTitleBar();
        LoadProfiles();

        profileUsageTimer.Tick += async (_, _) => await UpdateProfileUsagePopupAsync();
    }

    private void BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Height = 36,
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
            Text = "Multi WebView",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Location = new Point(36, 0),
            Size = new Size(Math.Max(120, Width - 164), 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        AttachTitleBarDrag(title);
        titleBar.Controls.Add(title);

        btnPin = CreateTitleButton("📌", TogglePin);
        titleBar.Controls.Add(btnPin);
        btnMax = CreateTitleButton("⬜", ToggleMaximize);
        titleBar.Controls.Add(btnMax);
        btnClose = CreateTitleButton("✕", MinimizeToTray, true);
        titleBar.Controls.Add(btnClose);
        titleBar.BringToFront();
    }

    private void ConfigureTrayIcon()
    {
        trayIcon.Text = "Multi WebView";
        trayIcon.Visible = false;
        trayIcon.Icon = WindowIdentity.CreatePickerIcon();

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

        var exitItem = new ToolStripMenuItem("Exit")
        {
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            Height = 32,
            Width = 156,
            Padding = new Padding(10, 0, 10, 0),
            TextAlign = ContentAlignment.MiddleLeft
        };
        exitItem.Click += (_, _) => ExitApplication();

        menu.Items.Add(restoreItem);
        menu.Items.Add(exitItem);
        trayIcon.ContextMenuStrip = menu;
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private static void SetPickerIcon(Form form)
    {
        form.Icon = WindowIdentity.CreatePickerIcon();
    }

    private Button CreateTitleButton(string text, Action onClick, bool isClose = false)
    {
        var button = new Button
        {
            Text = text,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(40, 36),
            Dock = DockStyle.Right,
            BackColor = btnNormal
        };

        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => onClick();
        button.MouseEnter += (_, _) => button.BackColor = isClose ? btnCloseHover : btnHover;
        button.MouseLeave += (_, _) => button.BackColor = button == btnPin && isPinned ? btnActive : btnNormal;

        return button;
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 52, 40, 28),
            RowCount = 5,
            ColumnCount = 1,
            BackColor = Color.FromArgb(20, 20, 20)
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "Select Profile",
            AutoSize = true,
            Font = new Font("Segoe UI", 28F, FontStyle.Bold),
            ForeColor = Color.White,
            Margin = new Padding(0, 0, 0, 4)
        };
        root.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "Choose a saved profile or add one to sign in with Google.",
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F),
            ForeColor = Color.FromArgb(180, 180, 180),
            Margin = new Padding(0, 0, 0, 28)
        };
        root.Controls.Add(subtitle, 0, 1);

        profileList.Dock = DockStyle.Fill;
        profileList.AutoScroll = true;
        profileList.WrapContents = true;
        profileList.BackColor = Color.FromArgb(20, 20, 20);
        root.Controls.Add(profileList, 0, 2);

        createMultiViewButton = new ActionButtonControl("Create multi-view", OpenMultiView)
        {
            Dock = DockStyle.Top,
            Height = 36,
            Margin = new Padding(0, 0, 0, 0),
            Visible = false
        };
        root.Controls.Add(createMultiViewButton, 0, 3);

        var addPanel = new TableLayoutPanel
        {
            Height = 84,
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 4,
            Margin = new Padding(0, 22, 0, 0),
            BackColor = Color.FromArgb(20, 20, 20)
        };
        addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        addPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        addPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176));
        root.Controls.Add(addPanel, 0, 4);

        var profileNameHost = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(220, 36),
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(12, 8, 12, 0),
            BackColor = Color.FromArgb(32, 32, 32),
            BorderStyle = BorderStyle.FixedSingle
        };

        profileNameTextBox.PlaceholderText = "Profile name";
        profileNameTextBox.Dock = DockStyle.Fill;
        profileNameTextBox.Multiline = true;
        profileNameTextBox.Margin = Padding.Empty;
        profileNameTextBox.BackColor = Color.FromArgb(32, 32, 32);
        profileNameTextBox.ForeColor = Color.White;
        profileNameTextBox.BorderStyle = BorderStyle.None;
        profileNameTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddProfile();
            }
        };
        profileNameHost.Controls.Add(profileNameTextBox);
        addPanel.Controls.Add(profileNameHost, 0, 0);

        var profileUrlHost = new Panel
        {
            Dock = DockStyle.Fill,
            MinimumSize = new Size(220, 36),
            Margin = new Padding(0, 12, 0, 0),
            Padding = new Padding(12, 8, 12, 0),
            BackColor = Color.FromArgb(32, 32, 32),
            BorderStyle = BorderStyle.FixedSingle
        };

        profileUrlTextBox.Text = ProfileStore.DefaultStartUrl;
        profileUrlTextBox.Dock = DockStyle.Fill;
        profileUrlTextBox.Multiline = true;
        profileUrlTextBox.Margin = Padding.Empty;
        profileUrlTextBox.BackColor = Color.FromArgb(32, 32, 32);
        profileUrlTextBox.ForeColor = Color.White;
        profileUrlTextBox.BorderStyle = BorderStyle.None;
        profileUrlTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddProfile();
            }
        };
        profileUrlHost.Controls.Add(profileUrlTextBox);
        addPanel.Controls.Add(profileUrlHost, 0, 1);
        addPanel.SetColumnSpan(profileUrlHost, 4);

        var addProfileButton = CreateActionButton("Add profile", AddProfile);
        addProfileButton.Margin = new Padding(10, 0, 0, 0);
        addPanel.Controls.Add(addProfileButton, 1, 0);

        var changeFolderButton = CreateActionButton("Change folder", ChangeProfileFolder);
        changeFolderButton.Margin = new Padding(10, 0, 0, 0);
        addPanel.Controls.Add(changeFolderButton, 2, 0);

        var storageButton = CreateActionButton("Open profile folder", OpenProfileFolder);
        storageButton.Margin = new Padding(10, 0, 0, 0);
        addPanel.Controls.Add(storageButton, 3, 0);
    }

    private Control CreateActionButton(string text, Action onClick)
    {
        return new ActionButtonControl(text, onClick);
    }

    private void LoadProfiles()
    {
        HideProfileUsagePopup();
        var profiles = profileStore.LoadProfiles();
        var profileIds = profiles.Select(profile => profile.Id).ToHashSet();
        selectedProfileIds.RemoveWhere(openProfileIds.Contains);

        profileList.SuspendLayout();
        try
        {
            if (profiles.Count == 0)
            {
                foreach (var view in profileCardViews.Values)
                {
                    view.Card.Dispose();
                }

                profileList.Controls.Clear();
                profileCards.Clear();
                profileCardViews.Clear();
                profileList.Controls.Add(CreateEmptyState());
                return;
            }

            foreach (Control control in profileList.Controls.Cast<Control>().Where(control => control is not ProfileCardPanel).ToList())
            {
                profileList.Controls.Remove(control);
                control.Dispose();
            }

            foreach (var id in profileCardViews.Keys.Where(id => !profileIds.Contains(id)).ToList())
            {
                if (profileCardViews.TryGetValue(id, out var removedView))
                {
                    profileList.Controls.Remove(removedView.Card);
                    removedView.Card.Dispose();
                }

                profileCardViews.Remove(id);
                profileCards.Remove(id);
            }

            for (var index = 0; index < profiles.Count; index++)
            {
                var profile = profiles[index];
                if (!profileCardViews.TryGetValue(profile.Id, out var view))
                {
                    var card = (Panel)CreateProfileCard(profile);
                    view = profileCardViews[profile.Id];
                    profileList.Controls.Add(card);
                }

                UpdateProfileCardView(view, profile);
                profileList.Controls.SetChildIndex(view.Card, index);
            }
        }
        finally
        {
            profileList.ResumeLayout();
        }
    }

    private Control CreateProfileCard(Profile profile)
    {
        ProfileCardView? view = null;
        var isOpen = IsProfileOpen(profile);
        var isInTray = IsProfileInTray(profile);
        var isKeepRunning = IsProfileKeepRunningInTray(profile);
        var card = new ProfileCardPanel
        {
            Width = 224,
            Height = 188,
            Margin = new Padding(0, 0, 16, 16),
            BackColor = isOpen ? profileCardOpen : profileCardNormal,
            Cursor = Cursors.Hand,
            Tag = profile
        };
        card.Click += (_, _) => ToggleProfileSelection(view!.Profile);
        profileCards[profile.Id] = card;

        var stateBadge = CreateStatusBadge(
            isOpen
                ? isInTray ? "TRAY" : "OPEN"
                : "OFF",
            isOpen
                ? isInTray ? Color.FromArgb(198, 104, 35) : Color.FromArgb(34, 139, 94)
                : Color.FromArgb(82, 82, 82),
            isInTray ? 52 : 54,
            new Point(10, 10),
            profile);
        card.Controls.Add(stateBadge);

        var keepRunningBadge = CreateStatusBadge(
            "KEEP RUNNING",
            Color.FromArgb(176, 58, 58),
            104,
            new Point(10, 36),
            profile,
            7.5F);
        keepRunningBadge.Visible = isKeepRunning;
        card.Controls.Add(keepRunningBadge);

        if (!isKeepRunning)
        {
            keepRunningBadge.SendToBack();
        }

        var avatar = new AvatarControl(GetInitials(profile.Name))
        {
            ForeColor = Color.White,
            BackColor = isOpen ? Color.FromArgb(42, 112, 84) : Color.FromArgb(70, 70, 70),
            Size = new Size(58, 58),
            Location = new Point(83, 68),
            Cursor = Cursors.Hand
        };
        avatar.Click += (_, _) => ToggleProfileSelection(view!.Profile);
        card.Controls.Add(avatar);

        var name = new Label
        {
            Text = profile.Name,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoEllipsis = true,
            Size = new Size(180, 24),
            Location = new Point(22, 132),
            Cursor = Cursors.Hand
        };
        name.Click += (_, _) => ToggleProfileSelection(view!.Profile);
        card.Controls.Add(name);

        var lastUsed = new Label
        {
            Text = isOpen
                ? isInTray
                    ? $"Tray - last used {profile.LastUsedAt.LocalDateTime:g}"
                    : $"Open - last used {profile.LastUsedAt.LocalDateTime:g}"
                : $"Last used {profile.LastUsedAt.LocalDateTime:g}",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = isOpen ? Color.FromArgb(173, 220, 193) : Color.FromArgb(155, 155, 155),
            AutoEllipsis = true,
            Size = new Size(200, 20),
            Location = new Point(12, 158),
            Cursor = Cursors.Hand
        };
        lastUsed.Click += (_, _) => ToggleProfileSelection(view!.Profile);
        card.Controls.Add(lastUsed);

        var editButton = new Button
        {
            Text = "✎",
            FlatStyle = FlatStyle.Flat,
            BackColor = isOpen ? Color.FromArgb(48, 48, 48) : Color.FromArgb(45, 45, 45),
            ForeColor = isOpen ? Color.FromArgb(170, 170, 170) : Color.White,
            Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold),
            Size = new Size(30, 26),
            Location = new Point(146, 10),
            Cursor = isOpen ? Cursors.No : Cursors.Hand
        };
        editButton.FlatAppearance.BorderColor = isOpen
            ? Color.FromArgb(82, 82, 82)
            : Color.FromArgb(75, 75, 75);
        editButton.FlatAppearance.BorderSize = 1;
        editButton.FlatAppearance.MouseOverBackColor = editButton.BackColor;
        editButton.FlatAppearance.MouseDownBackColor = editButton.BackColor;
        editButton.Click += (_, _) =>
        {
            if (IsProfileOpen(view!.Profile))
            {
                return;
            }

            EditProfile(view.Profile);
        };
        card.Controls.Add(editButton);

        var deleteButton = new Button
        {
            Text = "🗑",
            FlatStyle = FlatStyle.Flat,
            BackColor = isOpen ? Color.FromArgb(64, 47, 47) : Color.FromArgb(75, 35, 35),
            ForeColor = isOpen ? Color.FromArgb(170, 170, 170) : Color.White,
            Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold),
            Size = new Size(30, 26),
            Location = new Point(184, 10),
            Cursor = isOpen ? Cursors.No : Cursors.Hand
        };
        deleteButton.FlatAppearance.BorderColor = isOpen
            ? Color.FromArgb(100, 70, 70)
            : Color.FromArgb(120, 55, 55);
        deleteButton.FlatAppearance.BorderSize = 1;
        deleteButton.FlatAppearance.MouseOverBackColor = deleteButton.BackColor;
        deleteButton.FlatAppearance.MouseDownBackColor = deleteButton.BackColor;
        deleteButton.Click += (_, _) =>
        {
            if (IsProfileOpen(view!.Profile))
            {
                return;
            }

            DeleteProfile(view.Profile);
        };
        card.Controls.Add(deleteButton);

        var webViewModeButton = new Button
        {
            Text = profile.UseHighGpuWebViewArguments ? "GPU" : "DEF",
            FlatStyle = FlatStyle.Flat,
            BackColor = isOpen
                ? profile.UseHighGpuWebViewArguments
                    ? Color.FromArgb(36, 55, 76)
                    : Color.FromArgb(48, 48, 48)
                : profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(42, 72, 112)
                : Color.FromArgb(45, 45, 45),
            ForeColor = isOpen ? Color.FromArgb(170, 170, 170) : Color.White,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Size = new Size(68, 24),
            Location = new Point(146, 42),
            Cursor = isOpen ? Cursors.No : Cursors.Hand
        };
        webViewModeButton.FlatAppearance.BorderColor = isOpen
            ? profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(72, 98, 128)
                : Color.FromArgb(82, 82, 82)
            : profile.UseHighGpuWebViewArguments
            ? Color.FromArgb(78, 120, 170)
            : Color.FromArgb(75, 75, 75);
        webViewModeButton.FlatAppearance.BorderSize = 1;
        webViewModeButton.FlatAppearance.MouseOverBackColor = webViewModeButton.BackColor;
        webViewModeButton.FlatAppearance.MouseDownBackColor = webViewModeButton.BackColor;
        webViewModeButton.Click += (_, _) =>
        {
            if (IsProfileOpen(view!.Profile))
            {
                return;
            }

            ToggleProfileWebViewMode(view.Profile);
        };
        card.Controls.Add(webViewModeButton);

        card.MouseEnter += (_, _) =>
        {
            if (IsProfileOpen(view!.Profile))
            {
                ShowProfileUsagePopup(view.Profile, card);
            }
            else if (!selectedProfileIds.Contains(view.Profile.Id))
            {
                card.BackColor = profileCardHover;
            }
        };
        card.MouseLeave += (_, _) => ScheduleProfileUsagePopupHide(view!.Profile, card);
        foreach (Control child in card.Controls)
        {
            child.MouseEnter += (_, _) =>
            {
                if (IsProfileOpen(view!.Profile))
                {
                    ShowProfileUsagePopup(view.Profile, card);
                }
            };
            child.MouseLeave += (_, _) => ScheduleProfileUsagePopupHide(view!.Profile, card);
        }

        view = new ProfileCardView(
            profile,
            card,
            stateBadge,
            keepRunningBadge,
            avatar,
            name,
            lastUsed,
            editButton,
            deleteButton,
            webViewModeButton);
        card.Tag = view;
        profileCardViews[profile.Id] = view;
        UpdateProfileCardView(view, profile);

        return card;
    }

    private void UpdateProfileCardView(ProfileCardView view, Profile profile)
    {
        view.Profile = profile;
        view.Card.Tag = view;

        var isOpen = IsProfileOpen(profile);
        var isInTray = IsProfileInTray(profile);
        var isKeepRunning = IsProfileKeepRunningInTray(profile);
        view.Card.BackColor = isOpen ? profileCardOpen : selectedProfileIds.Contains(profile.Id) ? profileCardSelected : profileCardNormal;
        view.Card.Cursor = Cursors.Hand;

        view.StateBadge.Text = isOpen ? isInTray ? "TRAY" : "OPEN" : "OFF";
        view.StateBadge.BackColor = isOpen
            ? isInTray ? Color.FromArgb(198, 104, 35) : Color.FromArgb(34, 139, 94)
            : Color.FromArgb(82, 82, 82);
        view.StateBadge.Width = isInTray ? 52 : 54;

        view.KeepRunningBadge.Visible = isKeepRunning;
        if (isKeepRunning)
        {
            view.KeepRunningBadge.BringToFront();
        }

        view.Avatar.Text = GetInitials(profile.Name);
        view.Avatar.BackColor = isOpen ? Color.FromArgb(42, 112, 84) : Color.FromArgb(70, 70, 70);
        view.Avatar.Invalidate();

        view.NameLabel.Text = profile.Name;
        view.LastUsedLabel.Text = isOpen
            ? isInTray
                ? $"Tray - last used {profile.LastUsedAt.LocalDateTime:g}"
                : $"Open - last used {profile.LastUsedAt.LocalDateTime:g}"
            : $"Last used {profile.LastUsedAt.LocalDateTime:g}";
        view.LastUsedLabel.ForeColor = isOpen ? Color.FromArgb(173, 220, 193) : Color.FromArgb(155, 155, 155);

        ApplyProfileActionState(view.EditButton, isOpen, Color.FromArgb(45, 45, 45), Color.FromArgb(48, 48, 48));
        ApplyProfileActionState(view.DeleteButton, isOpen, Color.FromArgb(75, 35, 35), Color.FromArgb(64, 47, 47));

        view.WebViewModeButton.Text = profile.UseHighGpuWebViewArguments ? "GPU" : "DEF";
        view.WebViewModeButton.BackColor = isOpen
            ? profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(36, 55, 76)
                : Color.FromArgb(48, 48, 48)
            : profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(42, 72, 112)
                : Color.FromArgb(45, 45, 45);
        view.WebViewModeButton.ForeColor = isOpen ? Color.FromArgb(170, 170, 170) : Color.White;
        view.WebViewModeButton.Cursor = isOpen ? Cursors.No : Cursors.Hand;
        view.WebViewModeButton.FlatAppearance.BorderColor = isOpen
            ? profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(72, 98, 128)
                : Color.FromArgb(82, 82, 82)
            : profile.UseHighGpuWebViewArguments
                ? Color.FromArgb(78, 120, 170)
                : Color.FromArgb(75, 75, 75);
        view.WebViewModeButton.FlatAppearance.MouseOverBackColor = view.WebViewModeButton.BackColor;
        view.WebViewModeButton.FlatAppearance.MouseDownBackColor = view.WebViewModeButton.BackColor;
    }

    private static void ApplyProfileActionState(Button button, bool isOpen, Color closedBackColor, Color openBackColor)
    {
        button.BackColor = isOpen ? openBackColor : closedBackColor;
        button.ForeColor = isOpen ? Color.FromArgb(170, 170, 170) : Color.White;
        button.Cursor = isOpen ? Cursors.No : Cursors.Hand;
        button.FlatAppearance.BorderColor = isOpen ? Color.FromArgb(82, 82, 82) : Color.FromArgb(75, 75, 75);
        button.FlatAppearance.MouseOverBackColor = button.BackColor;
        button.FlatAppearance.MouseDownBackColor = button.BackColor;
    }

    private Label CreateStatusBadge(
        string text,
        Color backColor,
        int width,
        Point location,
        Profile profile,
        float fontSize = 8F)
    {
        var badge = new Label
        {
            Text = text,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = backColor,
            Size = new Size(width, 22),
            Location = location,
            Cursor = Cursors.Hand
        };
        badge.Click += (_, _) => ToggleProfileSelection(profile);
        return badge;
    }

    private static Control CreateEmptyState()
    {
        return new Label
        {
            Text = "No profiles yet. Add one below to start Google sign-in.",
            AutoSize = true,
            ForeColor = Color.FromArgb(170, 170, 170),
            Font = new Font("Segoe UI", 11F),
            Margin = new Padding(0, 0, 0, 16)
        };
    }

    private void AddProfile()
    {
        var profile = profileStore.CreateProfile(profileNameTextBox.Text, profileUrlTextBox.Text);
        profileNameTextBox.Clear();
        profileUrlTextBox.Text = ProfileStore.DefaultStartUrl;
        OpenProfilesInMultiView([profile], false);
    }

    private void ChangeProfileFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose where Multi WebView profiles should be saved.",
            SelectedPath = Directory.Exists(profileStore.AppDataPath)
                ? profileStore.AppDataPath
                : ProfileStore.DefaultProfilesPath
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        profileStore.ChangeProfileFolder(dialog.SelectedPath);
        LoadProfiles();
    }

    private void OpenProfileFolder()
    {
        Directory.CreateDirectory(profileStore.AppDataPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = profileStore.AppDataPath,
            UseShellExecute = true
        });
    }

    private void ToggleProfileWebViewMode(Profile profile)
    {
        var useHighGpuArguments = !profile.UseHighGpuWebViewArguments;
        profileStore.UpdateProfileWebViewMode(profile, useHighGpuArguments);
        if (openProfileWindows.TryGetValue(profile.Id, out var window) && !window.IsDisposed)
        {
            window.UpdateProfileWebViewMode(profile.Id, useHighGpuArguments);
        }

        LoadProfiles();
    }

    private void EditProfile(Profile profile)
    {
        if (IsProfileOpen(profile))
        {
            return;
        }

        var editedProfile = PromptForProfile(profile);
        if (editedProfile is null)
        {
            return;
        }

        profileStore.UpdateProfile(profile, editedProfile.Value.Name, editedProfile.Value.StartUrl);
        LoadProfiles();
    }

    private void DeleteProfile(Profile profile)
    {
        if (IsProfileOpen(profile))
        {
            return;
        }

        if (!ConfirmDeleteProfile(profile))
        {
            return;
        }

        profileStore.DeleteProfile(profile);
        selectedProfileIds.Remove(profile.Id);
        LoadProfiles();
        UpdateMultiViewButton();
    }

    private void ToggleProfileSelection(Profile profile)
    {
        if (IsProfileOpen(profile))
        {
            ActivateOpenProfileWindow(profile);
            return;
        }

        if (!selectedProfileIds.Add(profile.Id))
        {
            selectedProfileIds.Remove(profile.Id);
        }

        UpdateProfileCardSelection(profile);
        UpdateMultiViewButton();
    }

    private void UpdateProfileCardSelection(Profile profile)
    {
        if (!profileCards.TryGetValue(profile.Id, out var card))
        {
            return;
        }

        if (IsProfileOpen(profile))
        {
            card.BackColor = profileCardOpen;
            card.Cursor = Cursors.Hand;
            return;
        }

        card.Cursor = Cursors.Hand;
        card.BackColor = selectedProfileIds.Contains(profile.Id)
            ? profileCardSelected
            : profileCardNormal;
    }

    private void ShowProfileUsagePopup(Profile profile, Panel card)
    {
        if (!openProfileWindows.TryGetValue(profile.Id, out var window) || window.IsDisposed)
        {
            HideProfileUsagePopup();
            return;
        }

        hoveredOpenProfile = profile;
        hoveredOpenProfileCard = card;
        EnsureProfileUsagePopup();
        PositionProfileUsagePopup(card);

        if (profileUsagePopup is { Visible: false })
        {
            profileUsagePopup.Show(this);
        }

        profileUsageTimer.Start();
        _ = UpdateProfileUsagePopupAsync();
    }

    private void ScheduleProfileUsagePopupHide(Profile profile, Panel card)
    {
        BeginInvoke((MethodInvoker)(() =>
        {
            if (hoveredOpenProfile?.Id == profile.Id && hoveredOpenProfileCard == card && IsMouseOverControl(card))
            {
                return;
            }

            if (!IsProfileOpen(profile))
            {
                UpdateProfileCardSelection(profile);
            }

            HideProfileUsagePopup();
        }));
    }

    private async Task UpdateProfileUsagePopupAsync()
    {
        if (isUpdatingProfileUsage || hoveredOpenProfile is not { } profile || profileUsagePopup is null)
        {
            return;
        }

        if (!openProfileWindows.TryGetValue(profile.Id, out var window) || window.IsDisposed)
        {
            HideProfileUsagePopup();
            return;
        }

        isUpdatingProfileUsage = true;
        try
        {
            var snapshot = await window.GetProfileUsageAsync(profile.Id);
            if (snapshot is null)
            {
                return;
            }

            usageTitleLabel!.Text = snapshot.Value.ProfileName;
            usageStateLabel!.Text = snapshot.Value.State;
            usageCpuValueLabel!.Text = snapshot.Value.Cpu;
            usageMemoryValueLabel!.Text = snapshot.Value.Memory;
            usageGpuValueLabel!.Text = snapshot.Value.Gpu;
            usageGpuMemoryValueLabel!.Text = snapshot.Value.GpuMemory;
        }
        finally
        {
            isUpdatingProfileUsage = false;
        }
    }

    private void HideProfileUsagePopup()
    {
        profileUsageTimer.Stop();
        hoveredOpenProfile = null;
        hoveredOpenProfileCard = null;
        if (profileUsagePopup is not null)
        {
            profileUsagePopup.Hide();
        }
    }

    private void EnsureProfileUsagePopup()
    {
        if (profileUsagePopup is not null)
        {
            return;
        }

        profileUsagePopup = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Size = new Size(196, 162),
            BackColor = Color.FromArgb(18, 18, 18),
            Padding = new Padding(12),
            TopMost = TopMost
        };

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 6,
            ColumnCount = 2,
            BackColor = Color.FromArgb(18, 18, 18)
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 1));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        profileUsagePopup.Controls.Add(content);

        usageTitleLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleLeft
        };
        content.Controls.Add(usageTitleLabel, 0, 0);

        usageStateLabel = new Label
        {
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(130, 220, 170),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoEllipsis = true,
            TextAlign = ContentAlignment.MiddleRight
        };
        content.Controls.Add(usageStateLabel, 1, 0);

        var divider = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(64, 64, 64),
            Margin = Padding.Empty
        };
        content.Controls.Add(divider, 0, 1);
        content.SetColumnSpan(divider, 2);

        usageCpuValueLabel = AddUsageRow(content, 2, "CPU", Color.FromArgb(127, 199, 255));
        usageMemoryValueLabel = AddUsageRow(content, 3, "MEM", Color.FromArgb(255, 207, 90));
        usageGpuValueLabel = AddUsageRow(content, 4, "GPU", Color.FromArgb(255, 128, 213));
        usageGpuMemoryValueLabel = AddUsageRow(content, 5, "VRAM", Color.FromArgb(199, 140, 255));

        profileUsagePopup.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(64, 64, 64));
            e.Graphics.DrawRectangle(pen, 0, 0, profileUsagePopup.Width - 1, profileUsagePopup.Height - 1);
        };
    }

    private static Label AddUsageRow(TableLayoutPanel content, int row, string title, Color titleColor)
    {
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = titleColor,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        content.Controls.Add(titleLabel, 0, row);

        var valueLabel = new Label
        {
            Text = "--",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        };
        content.Controls.Add(valueLabel, 1, row);
        return valueLabel;
    }

    private void PositionProfileUsagePopup(Control card)
    {
        if (profileUsagePopup is null)
        {
            return;
        }

        var cardBounds = card.RectangleToScreen(card.ClientRectangle);
        var screen = Screen.FromControl(card).WorkingArea;
        var x = cardBounds.Right + 8;
        var y = cardBounds.Top + 8;

        if (x + profileUsagePopup.Width > screen.Right)
        {
            x = cardBounds.Left - profileUsagePopup.Width - 8;
        }

        if (y + profileUsagePopup.Height > screen.Bottom)
        {
            y = Math.Max(screen.Top, screen.Bottom - profileUsagePopup.Height);
        }

        profileUsagePopup.Location = new Point(Math.Max(screen.Left, x), Math.Max(screen.Top, y));
    }

    private static bool IsMouseOverControl(Control control)
    {
        return control.RectangleToScreen(control.ClientRectangle).Contains(Cursor.Position);
    }

    private void UpdateMultiViewButton()
    {
        if (createMultiViewButton is null)
        {
            return;
        }

        var count = selectedProfileIds.Count;
        createMultiViewButton.Visible = count > 0;
        createMultiViewButton.Text = count == 1
            ? "Create multi-view (1 profile)"
            : $"Create multi-view ({count} profiles)";
        createMultiViewButton.Invalidate();
    }

    private void OpenMultiView()
    {
        var selectedProfiles = profileStore
            .LoadProfiles()
            .Where(profile => selectedProfileIds.Contains(profile.Id) && !IsProfileOpen(profile))
            .ToList();

        OpenProfilesInMultiView(selectedProfiles, true);
    }

    private void OpenProfilesInMultiView(IReadOnlyCollection<Profile> profiles, bool clearSelectedProfiles)
    {
        if (profiles.Count == 0)
        {
            return;
        }

        var unopenedProfiles = profiles
            .Where(profile => !IsProfileOpen(profile))
            .ToList();

        if (unopenedProfiles.Count == 0)
        {
            return;
        }

        foreach (var profile in unopenedProfiles)
        {
            profileStore.MarkUsed(profile);
        }

        var browser = new MultiViewForm(unopenedProfiles, profileStore);
        TrackOpenWindow(browser, unopenedProfiles.Select(profile => profile.Id));
        browser.Show();

        if (clearSelectedProfiles)
        {
            selectedProfileIds.Clear();
        }

        LoadProfiles();
        UpdateMultiViewButton();
    }

    private bool IsProfileOpen(Profile profile)
    {
        return openProfileIds.Contains(profile.Id);
    }

    private bool IsProfileInTray(Profile profile)
    {
        return openProfileWindows.TryGetValue(profile.Id, out var window) &&
            !window.IsDisposed &&
            window.IsInTray;
    }

    private bool IsProfileKeepRunningInTray(Profile profile)
    {
        return openProfileWindows.TryGetValue(profile.Id, out var window) &&
            !window.IsDisposed &&
            window.IsKeepRunningInTray;
    }

    private void ActivateOpenProfileWindow(Profile profile)
    {
        HideProfileUsagePopup();
        if (openProfileWindows.TryGetValue(profile.Id, out var window) && !window.IsDisposed)
        {
            window.ActivateFromProfilePicker();
        }
    }

    private void TrackOpenWindow(MultiViewForm window, IEnumerable<string> profileIds)
    {
        var ids = profileIds.ToHashSet();
        openProfileIdsByWindow[window] = ids;
        foreach (var id in ids)
        {
            openProfileIds.Add(id);
            selectedProfileIds.Remove(id);
            openProfileWindows[id] = window;
        }

        LoadProfiles();
        UpdateMultiViewButton();

        openWindows.Add(window);
        window.TrayStateChanged += (_, _) =>
        {
            LoadProfiles();
            UpdateMultiViewButton();
        };

        window.ProfileMovedToWindow += (_, args) =>
        {
            openProfileIdsByWindow.TryGetValue(args.TargetWindow, out var targetIds);
            foreach (var profile in args.Profiles)
            {
                ids.Remove(profile.Id);
                targetIds?.Add(profile.Id);
                openProfileIds.Add(profile.Id);
                selectedProfileIds.Remove(profile.Id);
                openProfileWindows[profile.Id] = args.TargetWindow;
            }

            LoadProfiles();
            UpdateMultiViewButton();
        };

        window.ProfilePoppedOut += (_, args) =>
        {
            ids.Remove(args.Profile.Id);
            var poppedWindow = new MultiViewForm([args.Profile], profileStore);
            TrackOpenWindow(poppedWindow, [args.Profile.Id]);
            poppedWindow.Show();
            LoadProfiles();
            UpdateMultiViewButton();
        };

        window.FormClosed += (_, _) =>
        {
            openWindows.Remove(window);
            openProfileIdsByWindow.Remove(window);
            foreach (var id in ids)
            {
                openProfileIds.Remove(id);
                openProfileWindows.Remove(id);
            }

            window.Dispose();
            LoadProfiles();
            UpdateMultiViewButton();
        };
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
            var width = previousBounds.Width > 0 ? previousBounds.Width : Width;
            var height = previousBounds.Height > 0 ? previousBounds.Height : Height;
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

        UpdateMaxButtonIcon();
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
        if (profileUsagePopup is not null)
        {
            profileUsagePopup.TopMost = isPinned;
        }

        if (btnPin is not null)
        {
            btnPin.BackColor = isPinned ? btnActive : btnNormal;
            btnPin.Text = isPinned ? "📍" : "📌";
        }
    }

    private void MinimizeToTray()
    {
        HideProfileUsagePopup();
        if (isMinimizedToTray)
        {
            if (Visible)
            {
                ForceHideToTray();
            }

            return;
        }

        pendingTitleBarDragStart = null;
        ResetTitleButtonColors();
        ActiveControl = null;
        Capture = false;
        windowStateBeforeTray = WindowState == FormWindowState.Minimized
            ? FormWindowState.Normal
            : WindowState;
        isMinimizedToTray = true;
        trayIcon.Visible = true;
        ForceHideToTray();
    }

    private void ExitApplication()
    {
        Close();
    }

    private void RestoreFromTray()
    {
        if (!isMinimizedToTray)
        {
            return;
        }

        isMinimizedToTray = false;
        Show();
        WindowState = windowStateBeforeTray == FormWindowState.Minimized
            ? FormWindowState.Normal
            : windowStateBeforeTray;
        ResetTitleButtonColors();
        trayIcon.Visible = false;
        Activate();
        BringToFront();
    }

    private void ForceHideToTray()
    {
        Hide();
    }

    public void ActivateFromExternalLaunch()
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

    private void ResetTitleButtonColors()
    {
        if (btnPin is not null)
        {
            btnPin.BackColor = isPinned ? btnActive : btnNormal;
        }

        if (btnMax is not null)
        {
            btnMax.BackColor = btnNormal;
        }

        if (btnClose is not null)
        {
            btnClose.BackColor = btnNormal;
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (isMinimizedToTray && Visible)
        {
            ForceHideToTray();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            profileUsageTimer.Stop();
            profileUsageTimer.Dispose();
            profileUsagePopup?.Dispose();
            trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.F4))
        {
            ExitApplication();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private static string GetInitials(string name)
    {
        var parts = name
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(2)
            .Select(part => char.ToUpperInvariant(part[0]));

        var initials = string.Concat(parts);
        return string.IsNullOrWhiteSpace(initials) ? "?" : initials;
    }

    private static ProfileEditResult? PromptForProfile(Profile profile)
    {
        using var dialog = new Form
        {
            Text = "Edit profile",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 242),
            BackColor = Color.FromArgb(20, 20, 20),
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(1)
        };
        SetPickerIcon(dialog);

        var titleBar = new Panel
        {
            Height = 36,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        dialog.Controls.Add(titleBar);

        var titleLabel = new Label
        {
            Text = "Edit profile",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(14, 9)
        };
        titleBar.Controls.Add(titleLabel);

        var closeButton = new Button
        {
            Text = "✕",
            Dock = DockStyle.Right,
            Size = new Size(40, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.White
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => dialog.DialogResult = DialogResult.Cancel;
        closeButton.MouseEnter += (_, _) => closeButton.BackColor = Color.FromArgb(232, 17, 35);
        closeButton.MouseLeave += (_, _) => closeButton.BackColor = Color.FromArgb(28, 28, 28);
        titleBar.Controls.Add(closeButton);

        var label = new Label
        {
            Text = "Profile name",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 58)
        };
        dialog.Controls.Add(label);

        var nameTextBox = CreateDialogTextBox(profile.Name, new Point(24, 82), new Size(412, 24));
        dialog.Controls.Add(nameTextBox);

        var urlLabel = new Label
        {
            Text = "Base URL",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(24, 118)
        };
        dialog.Controls.Add(urlLabel);

        var urlTextBox = CreateDialogTextBox(profile.StartUrl, new Point(24, 142), new Size(412, 24));
        dialog.Controls.Add(urlTextBox);

        ProfileEditResult? result = null;

        void Save()
        {
            result = new ProfileEditResult(nameTextBox.Text, urlTextBox.Text);
            dialog.DialogResult = DialogResult.OK;
        }

        var saveButton = new ActionButtonControl("Save", Save)
        {
            Location = new Point(272, 194),
            Size = new Size(78, 32),
            Dock = DockStyle.None
        };
        dialog.Controls.Add(saveButton);

        var cancelButton = new ActionButtonControl("Cancel", () => dialog.DialogResult = DialogResult.Cancel)
        {
            Location = new Point(358, 194),
            Size = new Size(78, 32),
            Dock = DockStyle.None
        };
        dialog.Controls.Add(cancelButton);

        foreach (var textBox in new[] { nameTextBox, urlTextBox })
        {
            textBox.KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Save();
                }
            };
        }

        dialog.Shown += (_, _) =>
        {
            nameTextBox.Focus();
            nameTextBox.SelectAll();
        };

        return dialog.ShowDialog() == DialogResult.OK ? result : null;
    }

    private static bool ConfirmDeleteProfile(Profile profile)
    {
        using var dialog = new Form
        {
            Text = "Delete profile",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.None,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(460, 190),
            BackColor = Color.FromArgb(20, 20, 20),
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Padding(1),
            KeyPreview = true
        };
        SetPickerIcon(dialog);

        var titleBar = new Panel
        {
            Height = 36,
            Dock = DockStyle.Top,
            BackColor = Color.FromArgb(28, 28, 28)
        };
        dialog.Controls.Add(titleBar);

        var titleLabel = new Label
        {
            Text = "Delete profile",
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Location = new Point(14, 9)
        };
        titleBar.Controls.Add(titleLabel);

        var closeButton = new Button
        {
            Text = "✕",
            Dock = DockStyle.Right,
            Size = new Size(40, 36),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(28, 28, 28),
            ForeColor = Color.White
        };
        closeButton.FlatAppearance.BorderSize = 0;
        closeButton.Click += (_, _) => dialog.DialogResult = DialogResult.Cancel;
        closeButton.MouseEnter += (_, _) => closeButton.BackColor = Color.FromArgb(232, 17, 35);
        closeButton.MouseLeave += (_, _) => closeButton.BackColor = Color.FromArgb(28, 28, 28);
        titleBar.Controls.Add(closeButton);

        var message = new Label
        {
            Text = $"Delete profile \"{profile.Name}\" and its saved browser data?",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F),
            Location = new Point(24, 60),
            Size = new Size(412, 44)
        };
        dialog.Controls.Add(message);

        var detail = new Label
        {
            Text = "This cannot be undone.",
            ForeColor = Color.FromArgb(180, 180, 180),
            Font = new Font("Segoe UI", 9F),
            Location = new Point(24, 108),
            Size = new Size(412, 22)
        };
        dialog.Controls.Add(detail);

        var deleteButton = new ActionButtonControl("Delete", () => dialog.DialogResult = DialogResult.OK)
        {
            Location = new Point(272, 142),
            Size = new Size(78, 32),
            Dock = DockStyle.None
        };
        dialog.Controls.Add(deleteButton);

        var cancelButton = new ActionButtonControl("Cancel", () => dialog.DialogResult = DialogResult.Cancel)
        {
            Location = new Point(358, 142),
            Size = new Size(78, 32),
            Dock = DockStyle.None
        };
        dialog.Controls.Add(cancelButton);

        dialog.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                dialog.DialogResult = DialogResult.Cancel;
            }
        };

        dialog.Shown += (_, _) => cancelButton.Focus();

        return dialog.ShowDialog() == DialogResult.OK;
    }

    private static TextBox CreateDialogTextBox(string text, Point location, Size size)
    {
        return new TextBox
        {
            Text = text,
            Location = location,
            Size = size,
            BackColor = Color.FromArgb(32, 32, 32),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private readonly record struct ProfileEditResult(string Name, string StartUrl);

    private sealed class ProfileCardView(
        Profile profile,
        Panel card,
        Label stateBadge,
        Label keepRunningBadge,
        AvatarControl avatar,
        Label nameLabel,
        Label lastUsedLabel,
        Button editButton,
        Button deleteButton,
        Button webViewModeButton)
    {
        public Profile Profile { get; set; } = profile;

        public Panel Card { get; } = card;

        public Label StateBadge { get; } = stateBadge;

        public Label KeepRunningBadge { get; } = keepRunningBadge;

        public AvatarControl Avatar { get; } = avatar;

        public Label NameLabel { get; } = nameLabel;

        public Label LastUsedLabel { get; } = lastUsedLabel;

        public Button EditButton { get; } = editButton;

        public Button DeleteButton { get; } = deleteButton;

        public Button WebViewModeButton { get; } = webViewModeButton;
    }

    private sealed class ProfileCardPanel : Panel
    {
        public ProfileCardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private sealed class BufferedFlowLayoutPanel : FlowLayoutPanel
    {
        public BufferedFlowLayoutPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }

    private sealed class AvatarControl : Control
    {
        public AvatarControl(string initials)
        {
            Text = initials;
            Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var background = new SolidBrush(BackColor);
            e.Graphics.FillRectangle(background, ClientRectangle);

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPadding |
                TextFormatFlags.SingleLine);
        }
    }
}

internal sealed class DarkTrayMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color MenuBack = Color.FromArgb(28, 28, 28);
    private static readonly Color MenuBorder = Color.FromArgb(64, 64, 64);
    private static readonly Color ItemHover = Color.FromArgb(60, 60, 60);
    private static readonly Color TextColor = Color.White;
    private static readonly Color DisabledTextColor = Color.FromArgb(130, 130, 130);

    public DarkTrayMenuRenderer()
        : base(new DarkTrayMenuColorTable())
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
        var color = e.Item.Selected ? ItemHover : MenuBack;
        using var brush = new SolidBrush(color);
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        var textColor = e.Item.Enabled ? TextColor : DisabledTextColor;
        var textBounds = new Rectangle(
            12,
            0,
            Math.Max(0, e.Item.Width - 24),
            e.Item.Height);

        TextRenderer.DrawText(
            e.Graphics,
            e.Text,
            e.TextFont,
            textBounds,
            textColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
    }

    private sealed class DarkTrayMenuColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => MenuBack;
        public override Color MenuBorder => DarkTrayMenuRenderer.MenuBorder;
        public override Color MenuItemSelected => ItemHover;
        public override Color MenuItemBorder => ItemHover;
        public override Color ImageMarginGradientBegin => MenuBack;
        public override Color ImageMarginGradientMiddle => MenuBack;
        public override Color ImageMarginGradientEnd => MenuBack;
        public override Color SeparatorDark => DarkTrayMenuRenderer.MenuBorder;
        public override Color SeparatorLight => DarkTrayMenuRenderer.MenuBorder;
    }
}

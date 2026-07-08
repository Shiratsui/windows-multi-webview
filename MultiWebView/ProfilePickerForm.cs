using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MultiWebView;

public sealed class ProfilePickerForm : Form
{
    private const string GoogleSignInUrl = "https://accounts.google.com/";
    private const int WmNcButtonDown = 0xA1;
    private const int HtCaption = 0x2;

    private readonly ProfileStore profileStore = new();
    private readonly HashSet<string> selectedProfileIds = [];
    private readonly HashSet<string> openProfileIds = [];
    private readonly Dictionary<string, Panel> profileCards = [];
    private readonly List<Form> openWindows = [];
    private readonly FlowLayoutPanel profileList = new();
    private readonly TextBox profileNameTextBox = new();
    private ActionButtonControl? createMultiViewButton;
    private readonly Color btnNormal = Color.FromArgb(28, 28, 28);
    private readonly Color btnHover = Color.FromArgb(60, 60, 60);
    private readonly Color btnCloseHover = Color.FromArgb(232, 17, 35);

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
        SetFormIcon(this);

        BuildLayout();
        BuildTitleBar();
        LoadProfiles();
    }

    private void BuildTitleBar()
    {
        var titleBar = new Panel
        {
            Height = 36,
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
            Text = "Multi WebView",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(36, 9)
        };
        title.MouseDown += DragForm;
        titleBar.Controls.Add(title);

        titleBar.Controls.Add(CreateTitleButton("—", () => WindowState = FormWindowState.Minimized));
        titleBar.Controls.Add(CreateTitleButton("⬜", ToggleMaximize));
        titleBar.Controls.Add(CreateTitleButton("✕", Close, true));
        titleBar.BringToFront();
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
        button.MouseLeave += (_, _) => button.BackColor = btnNormal;

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
            Height = 36,
            Dock = DockStyle.Fill,
            RowCount = 1,
            ColumnCount = 4,
            Margin = new Padding(0, 22, 0, 0),
            BackColor = Color.FromArgb(20, 20, 20)
        };
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
        profileList.Controls.Clear();
        profileCards.Clear();
        selectedProfileIds.RemoveWhere(openProfileIds.Contains);

        foreach (var profile in profileStore.LoadProfiles())
        {
            profileList.Controls.Add(CreateProfileCard(profile));
        }

        if (profileList.Controls.Count == 0)
        {
            profileList.Controls.Add(CreateEmptyState());
        }
    }

    private Control CreateProfileCard(Profile profile)
    {
        var card = new Panel
        {
            Width = 210,
            Height = 156,
            Margin = new Padding(0, 0, 16, 16),
            BackColor = Color.FromArgb(30, 30, 30),
            Cursor = IsProfileOpen(profile) ? Cursors.No : Cursors.Hand,
            Tag = profile
        };
        card.Click += (_, _) => ToggleProfileSelection(profile);
        profileCards[profile.Id] = card;

        var avatar = new Label
        {
            Text = GetInitials(profile.Name),
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(70, 70, 70),
            Size = new Size(58, 58),
            Location = new Point(76, 38),
            Cursor = IsProfileOpen(profile) ? Cursors.No : Cursors.Hand
        };
        avatar.Click += (_, _) => ToggleProfileSelection(profile);
        card.Controls.Add(avatar);

        var name = new Label
        {
            Text = profile.Name,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoEllipsis = true,
            Size = new Size(180, 24),
            Location = new Point(15, 100),
            Cursor = IsProfileOpen(profile) ? Cursors.No : Cursors.Hand
        };
        name.Click += (_, _) => ToggleProfileSelection(profile);
        card.Controls.Add(name);

        var lastUsed = new Label
        {
            Text = $"Last used {profile.LastUsedAt.LocalDateTime:g}",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(155, 155, 155),
            AutoEllipsis = true,
            Size = new Size(186, 20),
            Location = new Point(12, 126),
            Cursor = IsProfileOpen(profile) ? Cursors.No : Cursors.Hand
        };
        lastUsed.Click += (_, _) => ToggleProfileSelection(profile);
        card.Controls.Add(lastUsed);

        var editButton = new Button
        {
            Text = "✎",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 10F, FontStyle.Bold),
            Size = new Size(30, 26),
            Location = new Point(132, 8),
            Cursor = Cursors.Hand
        };
        editButton.FlatAppearance.BorderColor = Color.FromArgb(75, 75, 75);
        editButton.FlatAppearance.BorderSize = 1;
        editButton.Click += (_, _) =>
        {
            EditProfile(profile);
        };
        card.Controls.Add(editButton);

        var deleteButton = new Button
        {
            Text = "🗑",
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(75, 35, 35),
            ForeColor = Color.White,
            Font = new Font("Segoe UI Symbol", 9F, FontStyle.Bold),
            Size = new Size(30, 26),
            Location = new Point(170, 8),
            Cursor = Cursors.Hand
        };
        deleteButton.FlatAppearance.BorderColor = Color.FromArgb(120, 55, 55);
        deleteButton.FlatAppearance.BorderSize = 1;
        deleteButton.Click += (_, _) =>
        {
            DeleteProfile(profile);
        };
        card.Controls.Add(deleteButton);

        card.MouseEnter += (_, _) =>
        {
            if (!selectedProfileIds.Contains(profile.Id) && !IsProfileOpen(profile))
            {
                card.BackColor = Color.FromArgb(42, 42, 42);
            }
        };
        card.MouseLeave += (_, _) => UpdateProfileCardSelection(profile);
        UpdateProfileCardSelection(profile);

        return card;
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
        var profile = profileStore.CreateProfile(profileNameTextBox.Text);
        profileNameTextBox.Clear();
        OpenProfile(profile, GoogleSignInUrl);
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

    private void EditProfile(Profile profile)
    {
        var newName = PromptForText("Edit profile", "Profile name", profile.Name);
        if (newName is null)
        {
            return;
        }

        profileStore.RenameProfile(profile, newName);
        LoadProfiles();
    }

    private void DeleteProfile(Profile profile)
    {
        var result = MessageBox.Show(
            $"Delete profile \"{profile.Name}\" and its saved browser data?",
            "Delete profile",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
        {
            return;
        }

        profileStore.DeleteProfile(profile);
        selectedProfileIds.Remove(profile.Id);
        LoadProfiles();
        UpdateMultiViewButton();
    }

    private void OpenProfile(Profile profile, string? launchUrl = null)
    {
        if (IsProfileOpen(profile))
        {
            return;
        }

        profileStore.MarkUsed(profile);

        var browser = new BrowserForm(profile, profileStore, launchUrl);
        TrackOpenWindow(browser, [profile.Id]);
        browser.Show(this);

        LoadProfiles();
    }

    private void ToggleProfileSelection(Profile profile)
    {
        if (IsProfileOpen(profile))
        {
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
            card.BackColor = Color.FromArgb(38, 38, 38);
            card.Cursor = Cursors.No;
            return;
        }

        card.Cursor = Cursors.Hand;
        card.BackColor = selectedProfileIds.Contains(profile.Id)
            ? Color.FromArgb(25, 70, 115)
            : Color.FromArgb(30, 30, 30);
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

        if (selectedProfiles.Count == 0)
        {
            return;
        }

        foreach (var profile in selectedProfiles)
        {
            profileStore.MarkUsed(profile);
        }

        var browser = new MultiViewForm(selectedProfiles, profileStore);
        TrackOpenWindow(browser, selectedProfiles.Select(profile => profile.Id));
        browser.Show(this);

        selectedProfileIds.Clear();
        LoadProfiles();
        UpdateMultiViewButton();
    }

    private bool IsProfileOpen(Profile profile)
    {
        return openProfileIds.Contains(profile.Id);
    }

    private void TrackOpenWindow(Form window, IEnumerable<string> profileIds)
    {
        var ids = profileIds.ToList();
        foreach (var id in ids)
        {
            openProfileIds.Add(id);
            selectedProfileIds.Remove(id);
        }

        LoadProfiles();
        UpdateMultiViewButton();

        openWindows.Add(window);
        window.FormClosed += (_, _) =>
        {
            openWindows.Remove(window);
            foreach (var id in ids)
            {
                openProfileIds.Remove(id);
            }

            window.Dispose();
            LoadProfiles();
            UpdateMultiViewButton();
        };
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
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
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

    private static string? PromptForText(string title, string labelText, string initialValue)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(360, 130),
            BackColor = Color.FromArgb(30, 30, 30)
        };

        var label = new Label
        {
            Text = labelText,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(16, 16)
        };
        dialog.Controls.Add(label);

        var textBox = new TextBox
        {
            Text = initialValue,
            Location = new Point(16, 42),
            Size = new Size(328, 24),
            BackColor = Color.FromArgb(45, 45, 45),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        dialog.Controls.Add(textBox);

        var saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new Point(188, 86),
            Size = new Size(74, 28)
        };
        dialog.Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(270, 86),
            Size = new Size(74, 28)
        };
        dialog.Controls.Add(cancelButton);

        dialog.AcceptButton = saveButton;
        dialog.CancelButton = cancelButton;
        textBox.SelectAll();

        return dialog.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
}

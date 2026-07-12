using System.Runtime.InteropServices;

namespace MultiWebView;

internal static class WindowIdentity
{
    private const int NotifyIconTextLimit = 63;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static string BuildMultiViewTitle(IReadOnlyList<Profile> profiles)
    {
        var names = string.Join(", ", profiles.Select(profile => profile.Name));
        return profiles.Count == 1
            ? $"Multi WebView - {names}"
            : $"Multi WebView - {profiles.Count} profiles - {names}";
    }

    public static string BuildTrayText(string text)
    {
        return text.Length <= NotifyIconTextLimit
            ? text
            : string.Concat(text.AsSpan(0, NotifyIconTextLimit - 1), "...");
    }

    public static Icon CreatePickerIcon()
    {
        return CreateBadgeIcon("P", Color.FromArgb(25, 70, 115));
    }

    public static Icon CreateMultiViewIcon(IReadOnlyList<Profile> profiles)
    {
        var badge = profiles.Count <= 9 ? profiles.Count.ToString() : "9+";
        return CreateBadgeIcon(badge, Color.FromArgb(34, 139, 94));
    }

    private static Icon CreateBadgeIcon(string badge, Color accent)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var background = new SolidBrush(Color.FromArgb(20, 20, 20));
        using var border = new Pen(accent, 3);
        graphics.FillRoundedRectangle(background, new Rectangle(2, 2, 28, 28), new Size(7, 7));
        graphics.DrawRoundedRectangle(border, new Rectangle(3, 3, 26, 26), new Size(6, 6));

        using var badgeBrush = new SolidBrush(accent);
        graphics.FillEllipse(badgeBrush, new Rectangle(11, 9, 18, 18));

        using var font = new Font("Segoe UI", badge.Length > 1 ? 8F : 10F, FontStyle.Bold, GraphicsUnit.Point);
        TextRenderer.DrawText(
            graphics,
            badge,
            font,
            new Rectangle(11, 9, 18, 18),
            Color.White,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.NoPadding |
            TextFormatFlags.SingleLine);

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }
}

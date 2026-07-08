namespace MultiWebView;

public sealed class ActionButtonControl : Control
{
    private readonly Action onClick;
    private readonly Color normalBackColor = Color.FromArgb(45, 45, 45);
    private readonly Color hoverBackColor = Color.FromArgb(62, 62, 62);
    private readonly Color borderColor = Color.FromArgb(75, 75, 75);
    private bool isHovered;

    public ActionButtonControl(string text, Action onClick)
    {
        this.onClick = onClick;

        Text = text;
        Dock = DockStyle.Fill;
        Cursor = Cursors.Hand;
        ForeColor = Color.White;
        BackColor = normalBackColor;
        Font = new Font("Segoe UI", 9.5F);
        Margin = Padding.Empty;

        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        isHovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        isHovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnClick(EventArgs e)
    {
        onClick();
        base.OnClick(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        using var background = new SolidBrush(isHovered ? hoverBackColor : normalBackColor);
        e.Graphics.FillRectangle(background, ClientRectangle);

        using var border = new Pen(borderColor);
        e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

        using var textBrush = new SolidBrush(ForeColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };

        e.Graphics.DrawString(Text, Font, textBrush, ClientRectangle, format);
    }
}

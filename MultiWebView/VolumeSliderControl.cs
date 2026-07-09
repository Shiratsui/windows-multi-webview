using System.ComponentModel;

namespace MultiWebView;

public sealed class VolumeSliderControl : Control
{
    private bool isDragging;
    private int minimum;
    private int maximum = 100;
    private int currentValue = 100;

    public event EventHandler? ValueChanged;

    public VolumeSliderControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.UserPaint,
            true);

        TabStop = false;
        Cursor = Cursors.Hand;
        MinimumSize = new Size(48, 18);
    }

    [DefaultValue(0)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int Minimum
    {
        get => minimum;
        set
        {
            minimum = value;
            if (maximum < minimum)
            {
                maximum = minimum;
            }

            Value = currentValue;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int Maximum
    {
        get => maximum;
        set
        {
            maximum = Math.Max(value, minimum);
            Value = currentValue;
            Invalidate();
        }
    }

    [DefaultValue(100)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
    public int Value
    {
        get => currentValue;
        set
        {
            var nextValue = Math.Clamp(value, minimum, maximum);
            if (currentValue == nextValue)
            {
                return;
            }

            currentValue = nextValue;
            Invalidate();
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override bool ShowFocusCues => false;

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);

        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        isDragging = true;
        Capture = true;
        SetValueFromPointer(e.X);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (isDragging)
        {
            SetValueFromPointer(e.X);
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);

        if (e.Button == MouseButtons.Left)
        {
            isDragging = false;
            Capture = false;
            SetValueFromPointer(e.X);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var trackHeight = 4;
        var knobSize = 10;
        var left = knobSize / 2;
        var right = Width - knobSize / 2;
        var centerY = Height / 2;
        var trackY = centerY - trackHeight / 2;
        var trackWidth = Math.Max(1, right - left);
        var percent = maximum == minimum
            ? 0f
            : (currentValue - minimum) / (float)(maximum - minimum);
        var knobX = left + trackWidth * percent;

        using var trackBrush = new SolidBrush(Color.FromArgb(70, 70, 70));
        using var fillBrush = new SolidBrush(Color.FromArgb(105, 165, 230));
        using var knobBrush = new SolidBrush(Color.WhiteSmoke);
        using var knobBorder = new Pen(Color.FromArgb(25, 25, 25));

        e.Graphics.FillRectangle(trackBrush, left, trackY, trackWidth, trackHeight);
        e.Graphics.FillRectangle(fillBrush, left, trackY, Math.Max(0, knobX - left), trackHeight);
        e.Graphics.FillEllipse(knobBrush, knobX - knobSize / 2f, centerY - knobSize / 2f, knobSize, knobSize);
        e.Graphics.DrawEllipse(knobBorder, knobX - knobSize / 2f, centerY - knobSize / 2f, knobSize, knobSize);
    }

    private void SetValueFromPointer(int x)
    {
        var knobSize = 10;
        var left = knobSize / 2f;
        var right = Math.Max(left + 1, Width - knobSize / 2f);
        var percent = Math.Clamp((x - left) / (right - left), 0f, 1f);
        Value = minimum + (int)Math.Round((maximum - minimum) * percent);
    }
}

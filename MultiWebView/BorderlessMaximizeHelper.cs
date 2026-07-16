using System.Runtime.InteropServices;

namespace MultiWebView;

internal static class BorderlessMaximizeHelper
{
    public const int WmGetMinMaxInfo = 0x0024;

    private const int MonitorDefaultToNearest = 0x00000002;

    public static void ApplyWorkingAreaMaxBounds(Form form, ref Message message)
    {
        var monitor = MonitorFromWindow(form.Handle, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(message.LParam);
        var monitorArea = monitorInfo.Monitor;
        var workArea = monitorInfo.Work;

        minMaxInfo.MaxPosition.X = workArea.Left - monitorArea.Left;
        minMaxInfo.MaxPosition.Y = workArea.Top - monitorArea.Top;
        minMaxInfo.MaxSize.X = workArea.Right - workArea.Left;
        minMaxInfo.MaxSize.Y = workArea.Bottom - workArea.Top;
        minMaxInfo.MinTrackSize.X = form.MinimumSize.Width;
        minMaxInfo.MinTrackSize.Y = form.MinimumSize.Height;

        Marshal.StructureToPtr(minMaxInfo, message.LParam, false);
        message.Result = IntPtr.Zero;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public PointStruct Reserved;
        public PointStruct MaxSize;
        public PointStruct MaxPosition;
        public PointStruct MinTrackSize;
        public PointStruct MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public int Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

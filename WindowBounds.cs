using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyPiggyBank;

// WindowStyle.None needs monitor work-area bounds in physical pixels, including
// monitors with a different DPI or a taskbar on any screen edge.
internal static class WindowBounds
{
    public static System.Windows.Rect WorkArea(Window window)
    {
        var handle=new WindowInteropHelper(window).Handle;var monitor=MonitorFromWindow(handle,2);
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        var target=PresentationSource.FromVisual(window)?.CompositionTarget;
        if(handle==0||monitor==0||target is null||!GetMonitorInfo(monitor,ref info))return SystemParameters.WorkArea;
        var transform=target.TransformFromDevice;
        return new System.Windows.Rect(transform.Transform(new System.Windows.Point(info.Work.Left,info.Work.Top)),transform.Transform(new System.Windows.Point(info.Work.Right,info.Work.Bottom)));
    }
    public static void Attach(Window window)
    {
        window.SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)?.AddHook(HandleMessage);
    }

    private static nint HandleMessage(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message != 0x0024) return 0; // WM_GETMINMAXINFO
        var monitor = MonitorFromWindow(hwnd, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == 0 || !GetMonitorInfo(monitor, ref info)) return 0;
        var limits = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        limits.MaxPosition = new Point { X = info.Work.Left - info.Monitor.Left, Y = info.Work.Top - info.Monitor.Top };
        limits.MaxSize = new Point { X = info.Work.Right - info.Work.Left, Y = info.Work.Bottom - info.Work.Top };
        // Keep WPF's minimum tracking size; cap maximization to this monitor's work area.
        limits.MaxTrackSize = limits.MaxSize;
        Marshal.StructureToPtr(limits, lParam, false); handled = true; return 0;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MinMaxInfo { public Point Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}

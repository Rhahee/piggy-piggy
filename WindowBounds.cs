using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MyPiggyBank;

// Dialog sizing uses the current monitor's work area in WPF units.
// Native Windows chrome handles main-window resizing and maximization.
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
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
}

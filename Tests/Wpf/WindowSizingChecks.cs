using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MyPiggyBank;

internal static class WindowSizingChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint SendMessage(nint hwnd, int message, nint wparam, nint lparam);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd, out Rect rect);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    public static async Task AcrossDpi(MainWindow window, Func<int, Task> inspect)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(MonitorFromWindow(handle, 2), ref info)) throw new Exception("Cannot read test monitor bounds.");
        foreach (var dpi in new[] { 96, 120, 144, 192, 96 })
        {
            var rect = new Rect { Left = info.Work.Left, Top = info.Work.Top, Right = info.Work.Left + 1440, Bottom = info.Work.Top + 860 };
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Rect>());
            try { Marshal.StructureToPtr(rect, pointer, false); SendMessage(handle, 0x02E0, (nint)(dpi | dpi << 16), pointer); }
            finally { Marshal.FreeHGlobal(pointer); }
            await window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
            if (Math.Abs(VisualTreeHelper.GetDpi(window).PixelsPerInchX - dpi) > 1) throw new Exception("WPF did not apply the simulated DPI transition.");
            if (!GetWindowRect(handle, out var actual) || actual.Left < info.Work.Left || actual.Top < info.Work.Top || actual.Right > info.Work.Right || actual.Bottom > info.Work.Bottom)
                throw new Exception($"The main window exceeds the monitor work area at {dpi} DPI.");
            await inspect(dpi);
        }
    }

    public static int Run()
    {
        var window = new MainWindow { ShowInTaskbar = false };
        Exception? failure = null;
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => {
            try
            {
                System.Windows.Input.ApplicationCommands.New.Execute(null, window);
                await AcrossDpi(window, _ => {
                    var status = (TextBlock)typeof(MainWindow).GetField("status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
                    var bounds = status.TransformToAncestor(window).TransformBounds(new System.Windows.Rect(0, 0, status.ActualWidth, status.ActualHeight));
                    if (status.ActualHeight <= 0 || bounds.Bottom > window.ActualHeight || bounds.Top < 0)
                        throw new Exception("The status bar is not pinned inside the viewport after the DPI change.");
                    return Task.CompletedTask;
                });
                Console.WriteLine("PASS main window fits monitor and status stays pinned through DPI transitions");
            }
            catch (Exception error) { failure = error; }
            finally { window.Close(); }
        }));
        window.ShowDialog();
        if (failure is not null) throw failure;
        return 1;
    }
}

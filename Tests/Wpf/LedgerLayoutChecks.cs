using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MyPiggyBank;
using MyPiggyBank.Core;

internal static class LedgerLayoutChecks
{
    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(root, i))) yield return child;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static Rect Bounds(FrameworkElement element, Window window) => element.TransformToAncestor(window).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
    private static Task Idle(MainWindow window) => window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle).Task;
    private static DataGrid Ledger(MainWindow window) => (DataGrid)typeof(MainWindow).GetField("ledger", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static DockPanel Layout(MainWindow window) => (DockPanel)((ContentControl)typeof(MainWindow).GetField("body", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!).Content;
    private static void Wheel(UIElement source, int delta) => source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, delta) { RoutedEvent = Mouse.PreviewMouseWheelEvent });
    private static void RunWindow(double width, double height, int banks, Func<MainWindow, Task> test)
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Piggy-layout-" + Guid.NewGuid().ToString("N") + ".mpb");
        var bank = new Bank(Guid.NewGuid(), "Fictional Bank Account", new(2026, 10, 1), 0);
        var category = new Category(Guid.NewGuid(), "Fictional Salary Category", CategoryKind.Income);
        var document = new FinanceDocument { Banks = [bank], Categories = [category] };
        for (var i = 1; i < banks; i++) document.Banks.Add(new(Guid.NewGuid(), "Fictional Bank " + i, new(2026, 10, 1), 0));
        for (var i = 0; i < 125; i++) document.Transactions.Add(new(Guid.NewGuid(), new(2026, 10, i % 28 + 1), TransactionKind.Income, bank.Id, null, category.Id, 100_000 + i, 0, null, "Fictional transaction description " + i));
        var session = new DocumentSession(); session.Apply(document); session.Save(file);
        var window = new MainWindow { Width = width, Height = height, ShowInTaskbar = false };
        Exception? failure = null;
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => {
            try { window.OpenPath(file); await Idle(window); await test(window); }
            catch (Exception error) { failure = error; }
            finally { window.Close(); }
        }));
        try { window.ShowDialog(); if (failure is not null) throw failure; }
        finally { System.IO.File.Delete(file); }
    }
    public static int Run()
    {
        RunWindow(1000, 640, 1, async window => {
            ApplicationCommands.Find.Execute(null, window); await Idle(window);
            var table = Ledger(window); var scroll = Visuals<ScrollViewer>(table).First();
            Require(scroll.ScrollableHeight > 0, "125 transactions expand beyond the viewport instead of scrolling inside the table.");
            Require(Visuals<DataGridRow>(table).Count() < 80, "The table lost row virtualization.");
            var layout = Layout(window);
            var chrome = layout.Children.Cast<FrameworkElement>().Take(3).ToArray();
            var before = chrome.Select(element => Bounds(element, window)).ToArray();
            var row = Visuals<DataGridRow>(table).First(); Wheel(row, -120); await Idle(window);
            Require(scroll.VerticalOffset > 0, "Mouse wheel did not scroll the transaction table.");
            scroll.ScrollToBottom(); scroll.ScrollToRightEnd(); await Idle(window);
            row = Visuals<DataGridRow>(table).First();
            var date = (TextBlock)table.Columns[0].GetCellContent(row);
            var dateBounds = Bounds(date, window); var tableBounds = Bounds(table, window);
            Require(date.Text.Length == 10 && date.ActualWidth >= 80 && dateBounds.Left >= tableBounds.Left && dateBounds.Right <= tableBounds.Right,
                "The full date disappears when the narrow table scrolls horizontally.");
            for (var i = 0; i < chrome.Length; i++) Require(Bounds(chrome[i], window) == before[i], "Ribbon, footer or page tabs move with table scrolling.");
            Wheel(row, 120); await Idle(window);
            Require(scroll.VerticalOffset < scroll.ScrollableHeight, "Mouse wheel did not scroll back up.");
        });
        Console.WriteLine("PASS 125-row ledger wheel scrolling keeps dates and window navigation visible");
        RunWindow(1360, 860, 1, async window => {
            ApplicationCommands.Find.Execute(null, window); await Idle(window);
            var table = Ledger(window);
            var used = table.Columns.Sum(column => column.ActualWidth);
            // A CI monitor can clamp the requested window below the column minima.
            // In that case horizontal overflow is intentional, not a layout failure.
            var minimum = table.Columns.Sum(column => column.MinWidth);
            Require(used >= Math.Max(minimum, table.ActualWidth - SystemParameters.VerticalScrollBarWidth - 5) - 2 &&
                used <= Math.Max(minimum, table.ActualWidth) + 2,
                $"Ledger columns do not fill their available width: columns={used}, viewport={table.ActualWidth}, minimum={minimum}.");
            Require(table.Columns.Single(column => Equals(column.Header, "Bank Account")).ActualWidth >= 160, "Bank column is too narrow.");
            Require(table.Columns.Single(column => Equals(column.Header, "Category")).ActualWidth >= 144, "Category column is too narrow.");
            Require(table.Columns.Single(column => Equals(column.Header, "Description")).ActualWidth >= 220, "Description column is too narrow.");
        });
        Console.WriteLine("PASS wide ledger distributes column widths across its viewport");
        RunWindow(1000, 640, 24, async window => {
            var layout = Layout(window); var chrome = layout.Children.Cast<FrameworkElement>().Take(3).ToArray();
            var before = chrome.Select(element => Bounds(element, window)).ToArray();
            var sidebar = (ScrollViewer)layout.Children[3];
            Wheel(Visuals<Button>(sidebar).First(), -120); await Idle(window);
            Require(sidebar.VerticalOffset > 0, "Mouse wheel cannot scroll a long bank list.");
            var table = Visuals<DataGrid>(layout).First();
            var page = Visuals<ScrollViewer>(layout).First(viewer => viewer.Content is StackPanel && viewer.ScrollableHeight > 0 && Visuals<DataGrid>(viewer).Contains(table));
            page.ScrollToBottom(); await Idle(window);
            var inner = Visuals<ScrollViewer>(table).First(); inner.ScrollToTop(); await Idle(window);
            var offset = page.VerticalOffset;
            Wheel(Visuals<DataGridRow>(table).First(), 120); await Idle(window);
            Require(page.VerticalOffset < offset, "Wheel over the recent table does not hand off to the overview page.");
            for (var i = 0; i < chrome.Length; i++) Require(Bounds(chrome[i], window) == before[i], "Overview scrolling moves navigation.");
        });
        Console.WriteLine("PASS overview and bank-list wheel scrolling keep navigation fixed");
        RunWindow(1360, 860, 1, async window => {
            ApplicationCommands.Find.Execute(null, window); await Idle(window);
            await WindowSizingChecks.AcrossDpi(window, async dpi => {
                var layout = Layout(window); var chrome = layout.Children.Cast<FrameworkElement>().Take(3).ToArray();
                var before = chrome.Select(element => Bounds(element, window)).ToArray();
                var table = Ledger(window); var scroll = Visuals<ScrollViewer>(table).First();
                scroll.ScrollToTop(); scroll.ScrollToRightEnd(); await Idle(window);
                var row = Visuals<DataGridRow>(table).First(); var date = (TextBlock)table.Columns[0].GetCellContent(row);
                Require(Bounds(date, window).Left >= Bounds(table, window).Left && date.ActualWidth >= 80, $"Date is clipped at {dpi} DPI.");
                Wheel(row, -120); await Idle(window);
                Require(scroll.VerticalOffset > 0, $"Wheel fails at {dpi} DPI.");
                for (var i = 0; i < chrome.Length; i++)
                {
                    var bounds = Bounds(chrome[i], window);
                    Require(bounds == before[i] && bounds.Top >= 0 && bounds.Bottom <= window.ActualHeight, $"Navigation is clipped or moves at {dpi} DPI.");
                }
            });
        });
        Console.WriteLine("PASS populated ledger keeps dates, wheel and navigation usable at 100/125/150/200 percent DPI");
        return 4;
    }
}

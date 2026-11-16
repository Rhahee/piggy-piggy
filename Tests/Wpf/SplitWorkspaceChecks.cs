using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MyPiggyBank;
using MyPiggyBank.Core;

internal static class SplitWorkspaceChecks
{
    private static object Field(MainWindow window, string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static Task Idle(MainWindow window) => window.Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle).Task;
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }

    private static void CheckPage(string page)
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Piggy-split-" + Guid.NewGuid().ToString("N") + ".mpb");
        var bank = new Bank(Guid.NewGuid(), "Fictional Bank", new(2026, 10, 1), 1_000_000);
        var category = new Category(Guid.NewGuid(), "Fictional Loan", CategoryKind.Income);
        var document = new FinanceDocument { Banks = [bank], Categories = [category] };
        document = WishlistService.Create(document, new(Guid.NewGuid(), "Fictional Wishlist", 100_000, null, WishlistPriority.Normal, ""), new(2026, 10, 1));
        var debt = new Debt(Guid.NewGuid(), "Fictional Debt", "Fictional Lender", null);
        document = DebtService.SaveTransaction(document, new(Guid.NewGuid(), new(2026, 10, 1), TransactionKind.Income, bank.Id, null, category.Id, 100_000, 0, null, "", DebtId: debt.Id), debt);
        var session = new DocumentSession(); session.Apply(document); session.Save(file);
        var original = System.IO.File.ReadAllBytes(file);
        var window = new MainWindow { Width = 1500, Height = 800, ShowInTaskbar = false };
        Exception? failure = null;
        window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () => {
            try
            {
                window.OpenPath(file);
                typeof(MainWindow).GetField("page", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, page);
                typeof(MainWindow).GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!.Invoke(window, null);
                await Idle(window);
                var layout = (DockPanel)((ContentControl)Field(window, "body")).Content;
                var viewport = layout.Children.OfType<ScrollViewer>().Last();
                var workspace = (DockPanel)((Grid)viewport.Content).Children[0];
                var split = workspace.Children.OfType<Grid>().Last();
                var wideWindowWidth = window.ActualWidth;
                // This is the preferred column width left by dragging the divider.
                split.ColumnDefinitions[0].Width = new GridLength(500);
                await Idle(window);
                var wideListWidth = split.ColumnDefinitions[0].ActualWidth;
                window.Width = 1000; await Idle(window);
                async Task CheckDetail()
                {
                    viewport.ScrollToRightEnd(); await Idle(window);
                    var detail = (ScrollViewer)Field(window, page == "Wishlist" ? "wishlistDetailScroll" : "debtDetailScroll");
                    var bounds = detail.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, detail.ActualWidth, detail.ActualHeight));
                    Require(bounds.Right <= viewport.ViewportWidth + 1, page + " detail right edge cannot be reached by scrolling.");
                    viewport.ScrollToLeftEnd(); await Idle(window);
                    bounds = detail.TransformToAncestor(viewport).TransformBounds(new Rect(0, 0, detail.ActualWidth, detail.ActualHeight));
                    Require(bounds.Left >= -1, page + " detail left edge cannot be reached by scrolling.");
                    Require(split.ColumnDefinitions[2].ActualWidth >= 380, page + " detail lost its minimum usable width.");
                }
                await CheckDetail();
                window.Width = wideWindowWidth; await Idle(window);
                Require(Math.Abs(split.ColumnDefinitions[0].ActualWidth - wideListWidth) < 1, "Widening the window lost the preferred list width.");
                // Exercise a logical viewport narrower than the detail minimum,
                // even when the local monitor is larger than the CI monitor.
                window.MinWidth = 450; window.Width = 500; await Idle(window);
                await CheckDetail();
                await WindowSizingChecks.AcrossDpi(window, _ => CheckDetail());
                Require(!((DocumentSession)Field(window, "session")).Dirty, "Resizing changed the financial document.");
                Require(original.SequenceEqual(System.IO.File.ReadAllBytes(file)), "Layout checks rewrote the document.");
            }
            catch (Exception error) { failure = error; }
            finally { window.Close(); }
        }));
        try { window.ShowDialog(); if (failure is not null) throw failure; }
        finally { System.IO.File.Delete(file); }
    }

    public static int Run()
    {
        foreach (var page in new[] { "Wishlist", "Debt" })
        {
            CheckPage(page);
            Console.WriteLine("PASS " + page + " split details remain reachable after divider resize, window resize and DPI transitions");
        }
        return 2;
    }
}

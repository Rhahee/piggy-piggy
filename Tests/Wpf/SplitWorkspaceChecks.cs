using System.Reflection;
using System.Windows;
using System.Windows.Automation;
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
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var item in Descendants<T>(child)) yield return item;
    }
    private static IEnumerable<T> Visuals<T>(DependencyObject element) where T : DependencyObject
    {
        if (element is T item) yield return item;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            foreach (var child in Visuals<T>(VisualTreeHelper.GetChild(element, i))) yield return child;
    }

    private static void CheckPage(string page)
    {
        var file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Piggy-split-" + Guid.NewGuid().ToString("N") + ".mpb");
        var bank = new Bank(Guid.NewGuid(), "Fictional Bank", new(2026, 10, 1), 1_000_000);
        var category = new Category(Guid.NewGuid(), "Fictional Loan", CategoryKind.Income);
        var document = new FinanceDocument { Banks = [bank], Categories = [category] };
        document = WishlistService.Create(document, new(Guid.NewGuid(), "Fictional Wishlist", 100_000, null, WishlistPriority.Normal, ""), new(2026, 10, 1));
        var debt = new Debt(Guid.NewGuid(), "Fictional Debt", "Fictional Lender", null, GroupName: "Gadgets");
        document = DebtService.SaveTransaction(document, new(Guid.NewGuid(), new(2026, 10, 1), TransactionKind.Income, bank.Id, null, category.Id, 100_000, 0, null, "", DebtId: debt.Id), debt);
        var other = debt with { Id = Guid.NewGuid(), Name = "Camera", GroupName = "gadgets" };
        document = DebtService.SaveTransaction(document, new(Guid.NewGuid(), new(2026, 10, 1), TransactionKind.Income, bank.Id, null, category.Id, 250_000, 0, null, "", DebtId: other.Id), other);
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
                if (page == "Debt")
                {
                    var list = (ScrollViewer)Field(window, "debtListScroll");
                    var group = Descendants<Button>(list).Single(button => Equals(button.Content, "Gadgets"));
                    group.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Idle(window);
                    var groupDetail = (ScrollViewer)Field(window, "debtDetailScroll");
                    Require(Descendants<TextBlock>(groupDetail).Any(text => text.Text == Display.Money(350_000)), "Group total must include both debts.");
                    var member = Descendants<Button>(groupDetail).Single(button => Equals(button.Content, "Camera"));
                    member.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await Idle(window);
                    Require(Equals(Field(window, "selectedDebt"), other.Id), "Clicking a group row must select the individual debt.");
                    var memberDetail = (ScrollViewer)Field(window, "debtDetailScroll");
                    Require(Descendants<Button>(memberDetail).Any(button => Equals(button.Content, "Record Payment") && button.IsEnabled), "Grouped debt must still support individual payment.");
                }
                Visuals<CheckBox>(window).Single(toggle => AutomationProperties.GetName(toggle) == "Dark theme").IsChecked = true;
                await Idle(window);
                var layout = (DockPanel)((ContentControl)Field(window, "body")).Content;
                var viewport = layout.Children.OfType<ScrollViewer>().Last();
                var workspace = (DockPanel)((Grid)viewport.Content).Children[0];
                var split = workspace.Children.OfType<Grid>().Last();
                var detailField = page == "Wishlist" ? "wishlistDetailScroll" : "debtDetailScroll";
                Require(((ScrollViewer)Field(window, detailField)).Parent is Border { Background: SolidColorBrush surface, Padding.Right: > 0 } &&
                    surface.Color == Color.FromRgb(0x25, 0x26, 0x32), page + " detail and its scrollbar must sit inside the dark card.");
                var listField = page == "Wishlist" ? "wishlistListScroll" : "debtListScroll";
                Require(((ScrollViewer)Field(window, listField)).Parent is Border { CornerRadius.TopLeft: 12, Padding.Right: > 0 },
                    page + " list and its scrollbar must sit inside a rounded card.");
                var wideWindowWidth = window.ActualWidth;
                // This is the preferred column width left by dragging the divider.
                split.ColumnDefinitions[0].Width = new GridLength(500);
                await Idle(window);
                var wideListWidth = split.ColumnDefinitions[0].ActualWidth;
                window.Width = 1000; await Idle(window);
                async Task CheckDetail()
                {
                    viewport.ScrollToRightEnd(); await Idle(window);
                    var detail = (ScrollViewer)Field(window, detailField);
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

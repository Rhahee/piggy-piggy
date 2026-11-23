using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private static Border RecentCard(MainWindow window)
    {
        var heading = Visuals<TextBlock>(Layout(window)).Single(text => text.Text == "Recent Transactions");
        return (Border)((StackPanel)heading.Parent).Parent;
    }
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
            var card = (Border)((DockPanel)((Grid)table.Parent).Parent).Parent;
            Require(card.Padding.Right == 12 && table.BorderThickness.Right == 0,
                "The ledger and its scrollbar must sit inside a padded card without a second frame.");
            Require(scroll.ScrollableHeight > 0, "125 transactions expand beyond the viewport instead of scrolling inside the table.");
            Require(Visuals<DataGridRow>(table).Count() < 80, "The table lost row virtualization.");
            var layout = Layout(window);
            var chrome = layout.Children.Cast<FrameworkElement>().Take(2).ToArray();
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
            if (!SystemParameters.HighContrast)
            {
                scroll.ScrollToTop();
                await Idle(window);
                var vertical = Visuals<ScrollBar>(scroll).Single(bar => bar.IsVisible && bar.Orientation == Orientation.Vertical);
                var track = (Track)vertical.Template.FindName("PART_Track", vertical);
                var down = (RepeatButton)vertical.Template.FindName("forward", vertical);
                Require(track.IsDirectionReversed && track.Thumb.ActualHeight > 0, "The themed vertical scrollbar must have a usable thumb.");
                ((RoutedCommand)down.Command).Execute(null, down);
                await Idle(window);
                Require(scroll.VerticalOffset > 0, "The themed scrollbar arrow must scroll the ledger.");
            }
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
            var layout = Layout(window); var chrome = layout.Children.Cast<FrameworkElement>().Take(2).ToArray();
            var before = chrome.Select(element => Bounds(element, window)).ToArray();
            var sidebar = (ScrollViewer)layout.Children[2];
            Wheel(Visuals<Button>(sidebar).First(), -120); await Idle(window);
            Require(sidebar.VerticalOffset > 0, "Mouse wheel cannot scroll a long bank list.");
            var recent = RecentCard(window);
            var page = Visuals<ScrollViewer>(layout).Single(viewer => viewer.Content is StackPanel && Visuals<Border>(viewer).Contains(recent));
            var workspace = (DockPanel)page.Parent;
            var header = (FrameworkElement)workspace.Children[0];
            var headerBounds = Bounds(header, window);
            Require(headerBounds.Height <= 48 && page.ScrollableHeight > 0, "Overview must have a compact subheader above its scrolling content.");
            page.ScrollToBottom(); await Idle(window);
            var offset = page.VerticalOffset;
            var entry = ((StackPanel)recent.Child).Children.OfType<Border>().Last();
            Wheel(entry, 120); await Idle(window);
            Require(page.VerticalOffset < offset && Bounds(header, window) == headerBounds,
                "Wheel over the recent list must scroll the page while keeping the Overview subheader fixed.");
            for (var i = 0; i < chrome.Length; i++) Require(Bounds(chrome[i], window) == before[i], "Overview scrolling moves navigation.");
        });
        Console.WriteLine("PASS overview and bank-list wheel scrolling keep navigation fixed");
        RunWindow(1180, 760, 2, async window => {
            Button ButtonNamed(string label) => Visuals<Button>(Layout(window)).First(button =>
                Equals(button.Content, label) || AutomationProperties.GetName(button) == label);
            async Task Click(string label)
            {
                ButtonNamed(label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await Idle(window);
            }
            CheckBox ThemeSwitch() => Visuals<CheckBox>(Layout(window)).Single(toggle => AutomationProperties.GetName(toggle) == "Dark theme");
            int RecentCount() => ((StackPanel)RecentCard(window).Child).Children.OfType<Border>().Count();
            Require(RecentCount() == 5, "Overview no longer displays five recent transactions.");
            var title = Visuals<TextBlock>(Layout(window)).First(text => text.Text == "Total Balance");
            var typeface = new Typeface(title.FontFamily, title.FontStyle, title.FontWeight, title.FontStretch);
            Require(typeface.TryGetGlyphTypeface(out var font) &&
                font.FontUri.ToString().Contains("SourceSans3-Semibold.ttf", StringComparison.OrdinalIgnoreCase),
                "Overview falls back to a system font instead of loading embedded Source Sans 3 Semibold.");

            await Click("Fictional Bank 1");
            Require(RecentCount() == 0 && Visuals<TextBlock>(RecentCard(window)).Any(text => text.Text == "No transactions in this period"),
                "Overview recent transactions must follow the account filter and show the empty state.");
            ThemeSwitch().IsChecked = true;
            await Idle(window);
            Require(Equals(Color.FromRgb(0x25, 0x26, 0x32), ((SolidColorBrush)RecentCard(window).Background).Color), "Overview list did not switch to the dark surface.");
            Require(Visuals<TextBlock>(Layout(window)).Any(text => text.Text == "Fictional Bank 1 Balance"), "Changing theme cleared the selected account.");
            await Click("All accounts");
            Require(RecentCount() == 5, "Clearing the account selection did not restore recent transactions.");
            await Click("Transactions");
            var darkSurface = Color.FromRgb(0x25, 0x26, 0x32);
            Require(ThemeSwitch().IsChecked == true &&
                ((SolidColorBrush)Ledger(window).Background).Color == darkSurface &&
                ((SolidColorBrush)Visuals<TextBox>(Layout(window)).Single().Background).Color == darkSurface,
                "Transactions must use the current theme for its ledger and search controls.");
            var filter = Visuals<ComboBox>(Layout(window)).Single();
            filter.IsDropDownOpen = true;
            await Idle(window);
            var dropdown = (Popup)filter.Template.FindName("PART_Popup", filter);
            var menu = (Border)dropdown.Child;
            Require(((SolidColorBrush)menu.Background).Color == darkSurface &&
                Visuals<ComboBoxItem>(menu).Any(item => item.Foreground is SolidColorBrush ink &&
                    ink.Color == Color.FromRgb(0xEF, 0xED, 0xF4)), "The account dropdown must remain readable in the dark workspace.");
            filter.IsDropDownOpen = false;
            ThemeSwitch().IsChecked = false;
            await Idle(window);
            Require(((SolidColorBrush)Ledger(window).Background).Color == Color.FromRgb(0xFD, 0xFC, 0xF9),
                "The theme must be changeable directly from Transactions.");
            ThemeSwitch().IsChecked = true;
            await Idle(window);
            foreach (var name in new[] { "Wishlist", "Debt" })
            {
                await Click(name);
                Require(ThemeSwitch().IsChecked == true && Visuals<Border>(Layout(window)).Any(border =>
                    border.CornerRadius.TopLeft == 12 && border.Background is SolidColorBrush brush && brush.Color == darkSurface),
                    name + " must use the current theme, including its empty state.");
            }
            await Click("Overview");
            Require(ThemeSwitch().IsChecked == true, "Navigating away reset the Overview theme.");
            ThemeSwitch().IsChecked = false;
            await Idle(window);
            Require(Equals(Color.FromRgb(0xFD, 0xFC, 0xF9), ((SolidColorBrush)RecentCard(window).Background).Color), "Overview list did not switch back to the light surface.");
            var ribbon = Layout(window).Children[0];
            var footer = Layout(window).Children[1];
            var status = Visuals<TextBlock>(ribbon).Single(text => text.Text == "125 transactions");
            Require(status.TextAlignment == TextAlignment.Right &&
                Visuals<Button>(ribbon).All(button => AutomationProperties.GetName(button) != "Save"),
                "The ribbon must show document status at the right in place of Save.");
            var version = Visuals<TextBlock>(footer).Single(text =>
                text.Text == AppVersion.Format(typeof(MainWindow).Assembly.GetName().Version!));
            var location = Visuals<TextBlock>(footer).Single(text => text.Text.EndsWith(".mpb", StringComparison.OrdinalIgnoreCase));
            var separators = Visuals<Border>(footer).Where(border => border.Width == 1 && border.Height == 16)
                .OrderBy(border => Bounds(border, window).Left).ToArray();
            var theme = ThemeSwitch();
            var update = ButtonNamed("Check for updates");
            var save = ButtonNamed("Save");
            Require(((Image)save.Content).Width == 14 && ((Image)update.Content).Width == 14 &&
                separators.Length == 2 &&
                Bounds(location, window).Right < Bounds(separators[0], window).Left &&
                Bounds(separators[0], window).Right < Bounds(version, window).Left &&
                Bounds(version, window).Right < Bounds(separators[1], window).Left &&
                Bounds(separators[1], window).Right <= Bounds(theme, window).Left &&
                Bounds(update, window).Left - Bounds(theme, window).Right >= 5 &&
                Bounds(update, window).Right < Bounds(save, window).Left,
                "The footer must show file location, separator, version, theme switch, update, then Save.");
            var overviewTab = ButtonNamed("Overview");
            var footerTabWidths = new[] { "Overview", "Transactions", "Wishlist", "Debt" }
                .Select(label => ButtonNamed(label).ActualWidth).ToList();
            Require(footerTabWidths.Max() - footerTabWidths.Min() < 1, "Footer tabs must have equal width.");
            Require(((Border)footer).BorderThickness.Top == 1 && Visuals<Button>(footer).Contains(overviewTab) &&
                Math.Abs((Bounds(overviewTab, window).Top + Bounds(overviewTab, window).Bottom) / 2 -
                         (Bounds(save, window).Top + Bounds(save, window).Bottom) / 2) < 1,
                "Page tabs and document controls must share one bordered footer.");
            Require(((Border)((StackPanel)ribbon).Children[0]).BorderThickness.Bottom == 1, "The ribbon tab row must show a bottom border.");
            Require(((ScrollViewer)Layout(window).Children[2]).Width == 160, "The Overview account sidebar must use its compact width.");
            var previous = ButtonNamed("Previous period");
            var next = ButtonNamed("Next period");
            var period = ((StackPanel)previous.Parent).Children.OfType<Button>().ElementAt(1);
            Require(previous.ActualHeight == period.ActualHeight && next.ActualHeight == period.ActualHeight &&
                Bounds(previous, window).Top == Bounds(period, window).Top,
                "The period picker must share the arrows' height and vertical alignment.");
            Require(Visuals<Button>(ribbon).All(button => !Equals(button.Content, "View") && !Equals(button.Content, "File") &&
                AutomationProperties.GetName(button) != "Document options"), "The ribbon must only show the current tabs and document action.");
            foreach (var label in new[] { "Income", "Expense", "Transfer", "Edit", "Delete", "Search", "Filter" })
            {
                var command = ButtonNamed(label);
                Require(command.Content is Image && Equals(command.ToolTip, label),
                    "Ribbon actions must be icon-only with an accessible name and tooltip.");
            }
            foreach (var label in new[] { "Home", "Data", "Overview", "Transactions", "Wishlist", "Debt" })
            {
                var tab = ButtonNamed(label);
                Require(((Border)tab.Template.FindName("surface", tab)).CornerRadius == new CornerRadius(0) &&
                    tab.BorderThickness.Right == 1, "Overview navigation tabs must have square corners and a right border.");
            }
            var start = ButtonNamed("Back to start screen");
            var home = ButtonNamed("Home");
            Require(start.Content is Image && Equals(start.ToolTip, "Back to start screen") &&
                ReferenceEquals(start.Parent, home.Parent) && Bounds(start, window).Right <= Bounds(home, window).Left &&
                Visuals<Button>(ribbon).All(button => AutomationProperties.GetName(button) != "New Piggy"),
                "The gear must be the first ribbon action and replace the new document button.");
            Require(((DockPanel)window.Content).Children[0] is Border { Height: 1, Background: not null } caption &&
                DockPanel.GetDock(caption) == Dock.Top, "A thin border must sit beneath the native title bar.");
            var account = ButtonNamed("All accounts");
            Require(Visuals<Button>((ScrollViewer)Layout(window).Children[2]).All(button => !Equals(button.Content, "Manage Banks")),
                "The sidebar must display only bank filters.");
            Require(((Border)account.Template.FindName("surface", account)).CornerRadius == new CornerRadius(0),
                "Overview account selections must have square corners.");
            Require(account.Margin.Left == 0 && account.Margin.Right == 0 &&
                Math.Abs(account.ActualWidth - ((StackPanel)account.Parent).ActualWidth) < 1,
                "The selected bank account must span the sidebar.");
            Require(!Visuals<DataGrid>(RecentCard(window)).Any() &&
                Visuals<TextBlock>(RecentCard(window)).Any(text =>
                    text.Inlines.OfType<System.Windows.Documents.Run>().Any(run => run.Text.Contains("Fictional Bank Account"))) &&
                Visuals<TextBlock>(RecentCard(window)).Any(text => text.Text.StartsWith("Rp ", StringComparison.Ordinal)),
                "Recent Transactions must show transaction details and amounts as a list.");
            var metrics = new[] { "Income", "Expenses", "Net Income" }.Select(label =>
            {
                var caption = Visuals<TextBlock>(ribbon).Single(text => text.Text == label);
                return (Border)((StackPanel)caption.Parent).Parent;
            }).ToArray();
            var metricBounds = metrics.Select(metric => Bounds(metric, window)).ToArray();
            Require(metricBounds.All(bounds => Math.Abs(bounds.Top - metricBounds[0].Top) < 1 &&
                        Math.Abs(bounds.Width - metricBounds[0].Width) < 1) &&
                    metricBounds[0].Right <= metricBounds[1].Left && metricBounds[1].Right <= metricBounds[2].Left &&
                    Math.Abs(metricBounds[2].Right - Bounds((StackPanel)ribbon, window).Right) < 1,
                "Period totals must share three equal-width sections in the right side of the ribbon.");

            var rows = new[] { "Wishlist Allocation", "Available Balance", "Fictional Bank 1" }.Select(label =>
                (Grid)Visuals<TextBlock>(Layout(window)).Single(text => text.Text == label &&
                    text.Parent is Grid grid && grid.ColumnDefinitions.Count == 3).Parent).ToList();
            foreach (var (column, left) in new[] { (0, true), (1, false), (2, false) })
            {
                var edges = rows.Select(row => Bounds((TextBlock)row.Children[column], window))
                    .Select(bounds => left ? bounds.Left : bounds.Right).ToList();
                Require(edges.Max() - edges.Min() < 1, "Summary labels, Rp, and figures must align within their card.");
            }
            foreach (var row in rows)
            {
                var band = (Border)row.Parent;
                DependencyObject ancestor = band;
                while (ancestor is not Border border || border.CornerRadius.TopLeft != 12)
                    ancestor = VisualTreeHelper.GetParent(ancestor);
                var cardBounds = Bounds((Border)ancestor, window);
                var bandBounds = Bounds(band, window);
                Require(band.Padding.Left == 28 && Math.Abs(bandBounds.Left - cardBounds.Left - 1) < 1 &&
                    Math.Abs(cardBounds.Right - bandBounds.Right - 1) < 1,
                    "The summary rules must reach the card border while keeping label padding.");
            }
            var indicator = Visuals<StackPanel>(footer).Single(panel => panel.GetType().Name == "UpdateIndicator");
            var updateSession = typeof(MainWindow).GetField("updates", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var background = update.Background;
            var outline = update.BorderBrush;
            Brush UpdateGlyph() => ((GeometryDrawing)((DrawingImage)((Image)update.Content).Source).Drawing).Brush;
            var glyph = UpdateGlyph();
            updateSession.GetType().GetProperty("Result")!.SetValue(updateSession, new UpdateCheck(UpdateStatus.UpToDate, "Up to date"));
            indicator.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(indicator, null);
            Require(Equals(background, update.Background) && Equals(outline, update.BorderBrush) && Equals(glyph, UpdateGlyph()),
                "The up-to-date indicator must keep its default colors.");
            await Click("Next period");
            var dirtySave = ButtonNamed("Save");
            var dirtyStatus = AutomationProperties.GetItemStatus(dirtySave);
            var dirtyAnimation = ((Image)dirtySave.Content).HasAnimatedProperties && dirtySave.HasAnimatedProperties;
            var dirtyTitle = window.Title;
            bool sawSavePrompt = false;
            _ = window.Dispatcher.InvokeAsync(() =>
            {
                var dialog = window.OwnedWindows.OfType<Window>().FirstOrDefault(owned => owned.IsVisible);
                if (dialog is null) return;
                var cancel = Visuals<Button>(dialog).FirstOrDefault(button => Equals(button.Content, "Cancel"));
                sawSavePrompt = cancel is not null;
                if (cancel is null) dialog.Close();
                else cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }, DispatcherPriority.ApplicationIdle);
            await Click("Back to start screen");
            var keptDocument = window.Title == dirtyTitle;
            await Click("Save");
            var savedButton = ButtonNamed("Save");
            Require(dirtyStatus == "Unsaved changes" && dirtyAnimation == SystemParameters.ClientAreaAnimation &&
                string.IsNullOrEmpty(AutomationProperties.GetItemStatus(savedButton)) && !((Image)savedButton.Content).HasAnimatedProperties,
                "Save must pulse only with pending changes and system animation enabled.");
            Require(sawSavePrompt && keptDocument, "Canceling the return to the start screen must preserve the document and its unsaved changes.");
            await Click("Data");
            Require(ButtonNamed("Accounts").Content is Image, "Bank management must remain accessible from the Data ribbon.");
            ThemeSwitch().IsChecked = true;
            await Idle(window);
            await Click("Back to start screen");
            Require(window.Title == "Piggy" && Visuals<Button>(window).Any(button => Equals(button.Content, "Open")),
                "The ribbon gear must return to the start screen.");
            CheckBox StartTheme() => Visuals<CheckBox>(window).Single(toggle => AutomationProperties.GetName(toggle) == "Dark theme");
            Button StartButton(string label) => Visuals<Button>(window).Single(button => Equals(button.Content, label));
            Image StartMascot() => Visuals<Image>(window).Single(image => AutomationProperties.GetName(image) == "Piggy");
            Require(StartTheme().IsChecked == true && Layout(window).Background.ToString() == "#FF191A23",
                "The start screen must retain the workspace's dark theme.");
            var darkLogo = StartMascot().Source;
            Require(Math.Abs(StartButton("New").ActualWidth - StartButton("Open").ActualWidth) <= 1 &&
                !Visuals<Button>(window).Any(button => AutomationProperties.GetName(button) == "Save"),
                "The start screen must use equal-width actions and hide the document-only Save control.");
            StartTheme().IsChecked = false;
            await Idle(window);
            Require(Layout(window).Background.ToString() == "#FFF7F5F0" && StartTheme().IsKeyboardFocusWithin,
                "Changing the start theme must update its surface and retain keyboard focus.");
            Require(!ReferenceEquals(darkLogo, StartMascot().Source) && StartMascot().ActualWidth == 320,
                "The theme must switch mascot assets at the same display size.");
            StartTheme().IsChecked = true;
            await Idle(window);
            StartButton("New").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Idle(window);
            Require(window.Title == "Untitled — Piggy" &&
                Visuals<TextBlock>(Layout(window)).Any(text => text.Text == "Add your first bank") && StartTheme().IsChecked == true,
                "New from the start screen must open a fresh document in Overview.");
            ApplicationCommands.Close.Execute(null, window);
            await Idle(window);
            Require(window.Title == "Piggy" && Visuals<Button>(window).Any(button => Equals(button.Content, "Open")),
                "Closing a document must return to the start screen.");
        });
        Console.WriteLine("PASS Overview theme and bundled font preserve account filtering across navigation");
        RunWindow(1360, 860, 1, async window => {
            ApplicationCommands.Find.Execute(null, window); await Idle(window);
            await WindowSizingChecks.AcrossDpi(window, async dpi => {
                var layout = Layout(window); var chrome = layout.Children.Cast<FrameworkElement>().Take(2).ToArray();
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
        return 5;
    }
}

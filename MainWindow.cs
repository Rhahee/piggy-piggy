using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow : Window
{
    private readonly DocumentSession session = new();
    private readonly DockPanel root = new();
    private readonly Border captionDivider = new() { Height = 1 };
    private readonly ContentControl body = new();
    private bool fittingWindow;
    private readonly UpdateSession updates = new();
    private TextBlock status = Ui.Text("", 12, Ui.Muted);
    private bool hasDocument, updating, preparingUpdate;
    private bool darkWorkspace;
    internal WorkspaceTheme Theme => darkWorkspace ? WorkspaceTheme.Dark : WorkspaceTheme.Light;
    private CheckBox? workspaceThemeSwitch;
    private Button? footerSave;
    private string page = "Overview", ribbonTab = "Home", search = "", typeFilter = "All types",debtTrackingFilter="All transactions";
    private Guid? selectedBank, categoryFilter, purposeFilter;
    private DateOnly month = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private ReportPeriodMode periodMode;
    private DateOnly customStart = new(DateTime.Today.Year,DateTime.Today.Month,1), customEnd = DateOnly.FromDateTime(DateTime.Today);
    private ReportPeriod SelectedPeriod => new(periodMode,month,customStart,customEnd);
    private DateRange SelectedRange => SelectedPeriod.Range(session.Document.PaySchedule);
    private DataGrid? ledger;
    private Button? editButton, deleteButton;
    private TextBox? searchBox;
    private string NameOfDocument => session.FilePath is null ? "Untitled" : Path.GetFileNameWithoutExtension(session.FilePath);
    public MainWindow()
    {
        Ui.ApplyWindowStyle(this);
        Width = Math.Min(1360, SystemParameters.WorkArea.Width - 48); Height = Math.Min(940, SystemParameters.WorkArea.Height - 48);
        MinWidth = Math.Min(1000, SystemParameters.WorkArea.Width); MinHeight = Math.Min(640, SystemParameters.WorkArea.Height); WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.SingleBorderWindow; ResizeMode = ResizeMode.CanResize;
        // Navigation stays outside every scrolling page. A bounded table can
        // virtualize rows and handle the wheel instead of growing the window.
        Content = root;
        DockPanel.SetDock(captionDivider, Dock.Top);
        root.Children.Add(captionDivider);
        root.Children.Add(body);
        body.PreviewMouseWheel += Ui.ScrollWithWheel;
        void ScheduleFit()
        {
            if (fittingWindow) return;
            fittingWindow = true;
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() => {
                try { if (IsLoaded) WindowBounds.FitMainWindow(this); }
                finally { fittingWindow = false; }
            }));
        }
        Loaded += (_, _) => ScheduleFit();
        DpiChanged += (_, _) => ScheduleFit();
        SourceInitialized += (_, _) => {
            var source = System.Windows.Interop.HwndSource.FromHwnd(new System.Windows.Interop.WindowInteropHelper(this).Handle);
            source?.AddHook((nint hwnd, int message, nint wparam, nint lparam, ref bool handled) => {
                // Wait for the drag to finish so clamping does not prevent crossing monitors.
                if (message is 0x0232 or 0x007E) ScheduleFit(); // WM_EXITSIZEMOVE / WM_DISPLAYCHANGE
                return nint.Zero;
            });
        };
        CommandBinding Bind(RoutedCommand command, Action action, Func<bool>? enabled = null) { var binding = new CommandBinding(command, (_, _) => action(), (_, e) => e.CanExecute = enabled?.Invoke() ?? true); CommandBindings.Add(binding); return binding; }
        Bind(ApplicationCommands.New, NewDocument); Bind(ApplicationCommands.Open, OpenDocument); Bind(ApplicationCommands.Save, () => Save(), () => hasDocument); Bind(ApplicationCommands.SaveAs, () => Save(true), () => hasDocument);
        Bind(ApplicationCommands.Close, CloseDocument, () => hasDocument);
        InputBindings.Add(new KeyBinding(ApplicationCommands.SaveAs, Key.S, ModifierKeys.Control | ModifierKeys.Shift));
        InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.W, ModifierKeys.Control));
        Bind(ApplicationCommands.Find, () => { page = "Transactions"; Render(); FocusSearch(); }, () => hasDocument);
        Closing += OnClosing;
        Closed += (_,_) => updates.Dispose();
        Render();
    }
    private void UpdateTitle()
    {
        Title = hasDocument ? $"{NameOfDocument}{(session.Dirty ? "*" : "")} — Piggy" : "Piggy";
        var count = session.Document.Transactions.Count;
        status.Text = hasDocument ? $"{count} {(count == 1 ? "transaction" : "transactions")}" : "";
        var saveStatus = session.Dirty ? "Unsaved changes" : session.FilePath is null ? "Not saved yet" : "Saved";
        status.ToolTip = hasDocument ? saveStatus : null;
        UpdateFooterSave();
    }
    private void Render()
    {
        CaptureWishlistViewport();
        CaptureDebtViewport();
        var theme = Theme;
        captionDivider.Background = theme.Rule;
        status = theme.Label("", 12, theme.Muted);
        workspaceThemeSwitch = null;
        footerSave = null;
        UpdateTitle(); ledger = null; editButton = null; deleteButton = null; searchBox = null;
        if (!hasDocument) { body.Content = StartPage(theme); return; }
        if(page=="Wishlist"){
            var visible=session.Document.Wishlists.Where(w=>selectedBank is null||session.Document.WishlistAllocated(w.Id,selectedBank)>0).ToList();
            if(!visible.Any(w=>w.Id==selectedWishlist))selectedWishlist=visible.FirstOrDefault()?.Id;
        }
        if(page=="Debt"&&!VisibleDebts().Any(d=>d.Id==selectedDebt))selectedDebt=VisibleDebts().FirstOrDefault()?.Id;
        var layout = new DockPanel();
        theme.ApplyTo(layout);
        var ribbon = BuildRibbon(theme); DockPanel.SetDock(ribbon, Dock.Top); layout.Children.Add(ribbon);
        var footer = BuildFooter(theme); DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var side = new ScrollViewer { Content = BuildSidebar(theme), Width = 160, VerticalContentAlignment = VerticalAlignment.Stretch, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        DockPanel.SetDock(side, Dock.Left); layout.Children.Add(side);
        var workspace = page == "Overview" ? Overview(theme) : page == "Wishlist" ? WishlistPage() : page=="Debt"?DebtPage():TransactionsPage();
        layout.Children.Add(page is "Wishlist" or "Debt" ? Ui.WorkspaceViewport(workspace) : workspace); body.Content = layout;
        if (editButton is not null) editButton.IsEnabled = false; if (deleteButton is not null) deleteButton.IsEnabled = false;
    }
    private Border BuildFooter(WorkspaceTheme theme)
    {
        var controls = Ui.Stack(Orientation.Horizontal);
        controls.HorizontalAlignment = HorizontalAlignment.Right;
        controls.VerticalAlignment = VerticalAlignment.Center;
        Border Separator(double gap) => new()
        {
            Width = 1, Height = 16, Background = theme.Outline,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(gap, 0, gap, 0)
        };
        if (hasDocument) controls.Children.Add(Separator(12));
        var version = UpdateIndicator.VersionLabel(theme.Muted);
        controls.Children.Add(version);
        controls.Children.Add(Separator(6));
        var themeSwitch = new CheckBox
        {
            Style = (Style)FindResource("WorkspaceThemeSwitch"), IsChecked = darkWorkspace,
            Width = 40, Height = 28, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            Foreground = theme.Accent, Background = theme.Wash, BorderBrush = theme.Outline,
            ToolTip = "Dark theme"
        };
        AutomationProperties.SetName(themeSwitch, "Dark theme");
        workspaceThemeSwitch = themeSwitch;
        RoutedEventHandler changeTheme = (_, _) => {
            darkWorkspace = themeSwitch.IsChecked == true;
            Render();
            var toggle = workspaceThemeSwitch;
            Dispatcher.InvokeAsync(() => toggle?.Focus(), System.Windows.Threading.DispatcherPriority.Input);
        };
        themeSwitch.Checked += changeTheme;
        themeSwitch.Unchecked += changeTheme;
        controls.Children.Add(themeSwitch);
        controls.Children.Add(new UpdateIndicator(this, updates, theme, showVersion: false));

        var footer = new Border
        {
            Height = 36, Padding = new Thickness(0, 0, 12, 0), Child = controls,
            Background = theme.Sidebar,
            BorderBrush = theme.Outline, BorderThickness = new Thickness(0, 1, 0, 0)
        };
        if (!hasDocument) return footer;

        var tabs = new UniformGrid { Rows = 1 };
        foreach (var name in new[] { "Overview", "Transactions", "Wishlist", "Debt" })
        {
            var button = Ui.Button(name, () => { page = name; Render(); });
            button.Padding = new Thickness(20,8,20,8);
            button.Margin = new Thickness(0);
            button.FontSize = 12;
            theme.StyleButton(button, selected: page == name, square: true);
            button.BorderBrush = theme.Outline;
            button.BorderThickness = new Thickness(0, 0, 1, 0);
            tabs.Children.Add(button);
        }

        var save = Ui.Button("", () => Save());
        var saveIcon = new Image { Width = 14, Height = 14, Stretch = Stretch.Uniform };
        save.Content = saveIcon;
        save.Unloaded += (_, _) =>
        {
            save.BeginAnimation(Control.BackgroundProperty, null);
            saveIcon.BeginAnimation(Image.SourceProperty, null);
        };
        save.Width = save.Height = save.MinHeight = 28;
        save.Padding = new Thickness(0);
        theme.StyleButton(save, outlined: true);

        save.VerticalAlignment = VerticalAlignment.Center;
        save.Margin = new Thickness(4, 0, 0, 0);
        AutomationProperties.SetName(save, "Save");
        footerSave = save;
        UpdateFooterSave();
        controls.Children.Add(save);
        var location = session.FilePath ?? "Not saved yet";
        var path = Ui.Text(location, 12, theme.Muted);
        path.HorizontalAlignment = HorizontalAlignment.Right;
        path.TextWrapping = TextWrapping.NoWrap;
        path.TextTrimming = TextTrimming.CharacterEllipsis;
        path.MaxWidth = 520;
        path.ToolTip = new TextBlock { Text = location, TextWrapping = TextWrapping.Wrap, MaxWidth = 600 };
        ToolTipService.SetShowDuration(path, 60000);
        var content = new DockPanel();
        footer.Child = content;
        DockPanel.SetDock(tabs, Dock.Left);
        DockPanel.SetDock(controls, Dock.Right);
        content.Children.Add(tabs);
        content.Children.Add(controls);
        content.Children.Add(path);
        return footer;
    }
    private void UpdateFooterSave()
    {
        if (footerSave is not { Content: Image icon } save) return;
        var dirty = session.Dirty;
        var theme = Theme;
        var warning = theme.Error;
        var background = Brushes.Transparent;
        save.Background = dirty ? warning : background;
        icon.Source = Ui.Drawing("save", dirty ? theme.Surface : theme.Accent);
        AutomationProperties.SetItemStatus(save, dirty ? "Unsaved changes" : "");
        save.ToolTip = dirty ? "Unsaved changes · Save (Ctrl+S)" : "Save (Ctrl+S)";
        var animate = dirty && SystemParameters.ClientAreaAnimation;
        if (animate == icon.HasAnimatedProperties) return;
        ObjectAnimationUsingKeyFrames? Blink(object inverted, object normal) => animate ? new ObjectAnimationUsingKeyFrames
        {
            Duration = TimeSpan.FromSeconds(1.6),
            RepeatBehavior = RepeatBehavior.Forever,
            KeyFrames =
            {
                new DiscreteObjectKeyFrame(inverted, KeyTime.FromTimeSpan(TimeSpan.Zero)),
                new DiscreteObjectKeyFrame(normal, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(.8)))
            }
        } : null;
        save.BeginAnimation(Control.BackgroundProperty, Blink(warning, background));
        icon.BeginAnimation(Image.SourceProperty, Blink(icon.Source, Ui.Drawing("save", warning)));
    }
    private UIElement StartPage(WorkspaceTheme theme)
    {
        var layout = new DockPanel();
        theme.ApplyTo(layout);
        layout.Background = theme.Background;
        var footer = BuildFooter(theme);
        DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(footer);

        var panel = Ui.Stack();
        const double logoSize = 320;
        var logo = new Image
        {
            Source = Ui.BrandImage(darkWorkspace ? "Brand/piggy-logo-dark.png" : "Brand/piggy-logo.png"),
            Width = logoSize, Height = logoSize,
            Clip = new RectangleGeometry(new Rect(0, 0, logoSize, logoSize), 12, 12)
        };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality);
        AutomationProperties.SetName(logo, "Piggy");
        panel.Children.Add(logo);

        var actions = new UniformGrid { Rows = 1, Margin = new Thickness(0, 24, 0, 0) };
        foreach (var (label, action, primary) in new (string, Action, bool)[]
                 { ("New", NewDocument, true), ("Open", OpenDocument, false) })
        {
            var button = theme.ActionButton(label, action, primary);
            button.Height = 42;
            button.FontSize = 16;
            button.Margin = new Thickness(primary ? 0 : 4, 0, primary ? 4 : 0, 0);
            button.ToolTip = primary ? "Create a Piggy document (Ctrl+N)" : "Open a Piggy document (Ctrl+O)";
            actions.Children.Add(button);
        }
        panel.Children.Add(actions);
        var card = theme.Card(panel, new Thickness(28));
        card.HorizontalAlignment = HorizontalAlignment.Center;
        card.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(card);
        return layout;
    }
    private UIElement BuildRibbon(WorkspaceTheme theme)
    {
        var stack = Ui.Stack(); stack.Background = theme.Sidebar; var tabs = Ui.Stack(Orientation.Horizontal);
        var start = Ui.Button("", CloseDocument);
        start.Content = Ui.Icon("settings", 18, theme.Accent);
        start.ToolTip = "Back to start screen";
        AutomationProperties.SetName(start, "Back to start screen");
        start.Width = 44;
        start.Height = 32;
        start.Margin = start.Padding = new Thickness(0);
        theme.StyleButton(start, square: true);
        start.BorderBrush = theme.Outline;
        start.BorderThickness = new Thickness(0, 0, 1, 0);
        tabs.Children.Add(start);
        foreach (var name in new[] { "Home", "Data" })
        {
            var button = Ui.Button(name, () => { ribbonTab = name; Render(); });
            button.Margin = new Thickness(0);
            button.Padding = new Thickness(20,0,20,0);
            button.Height = 32;
            button.MinHeight = 0;
            button.FontSize = 12;
            theme.StyleButton(button, selected: ribbonTab == name, square: true);
            button.BorderBrush = theme.Outline;
            button.BorderThickness = new Thickness(0, 0, 1, 0);
            tabs.Children.Add(button);
        }
        var tabRow = new DockPanel { Height = 32 };
        DockPanel.SetDock(tabs, Dock.Left);
        tabRow.Children.Add(tabs);
        status.Margin = new Thickness(12, 0, 12, 0);
        status.TextWrapping = TextWrapping.NoWrap;
        status.TextTrimming = TextTrimming.CharacterEllipsis;
        status.TextAlignment = TextAlignment.Right;
        tabRow.Children.Add(status);
        stack.Children.Add(new Border
        {
            Child = tabRow, BorderBrush = theme.Outline,
            BorderThickness = new Thickness(0, 0, 0, 1)
        });
        var groups = Ui.Stack(Orientation.Horizontal);
        void Group(params Button[] buttons)
        {
            var row = Ui.Stack(Orientation.Horizontal);
            foreach (var button in buttons)
            {
                var command = (StackPanel)button.Content;
                var icon = (Image)command.Children[0];
                var label = ((TextBlock)command.Children[1]).Text;
                command.Children.Remove(icon);
                icon.Width = icon.Height = 26;
                icon.Margin = new Thickness(0);
                button.Content = icon;
                button.Margin = new Thickness(0);
                button.Width = button.Height = 48;
                button.Padding = new Thickness(0);
                button.ToolTip = label;
                ToolTipService.SetShowOnDisabled(button, true);
                AutomationProperties.SetName(button, label);
                theme.StyleButton(button);
                row.Children.Add(button);
            }
            groups.Children.Add(new Border
            {
                Child = row, BorderBrush = theme.Outline,
                BorderThickness = new Thickness(0, 0, 1, 0), Padding = new Thickness(4)
            });
        }
        if(ribbonTab=="Home"&&page=="Debt")
        {
            var active=ActiveDebt;var remaining=active is null?0:session.Document.DebtAmounts(active.Id).Remaining;
            Button Command(string icon,string label,string action,bool enabled=true){var b=Ui.IconButton(icon,label,()=>DebtAction(action),theme.Accent);b.IsEnabled=enabled;return b;}
            Group(Command("add","New Debt","new"),Command("edit","Edit Details","edit",active is not null),Command("delete","Remove","remove",active is not null));
            Group(Command("arrow_up",active?.Kind==DebtKind.Self?"Add Debt":"Borrow","borrow",active is {IsPaid:false}),Command("arrow_down","Payment","payment",active is {IsPaid:false}&&remaining>0));
            Group(Command("square","Mark Paid","paid",active is {IsPaid:false}&&remaining==0),Command("arrow_sync","Reopen","reopen",active is {IsPaid:true}));
        }
        else if (ribbonTab == "Home" && page == "Wishlist")
        {
            var active=ActiveWishlist;var state=active is null?WishlistStatus.Cancelled:session.Document.WishlistState(active.Id);var funds=active is null?0:session.Document.WishlistAllocated(active.Id);
            Button Command(string icon,string text,string action,bool enabled=true){var b=Ui.IconButton(icon,text,()=>WishlistAction(action),theme.Accent);b.IsEnabled=enabled;return b;}
            Group(Command("add","New Wishlist","new"),Command("edit","Edit","edit",state==WishlistStatus.Planned),Command("dismiss","Cancel","cancel",state==WishlistStatus.Planned));
            Group(Command("add","Allocate","allocate",state==WishlistStatus.Planned),Command("arrow_swap","Move","move",state==WishlistStatus.Planned&&funds>0),Command("subtract","Release","release",state==WishlistStatus.Planned&&funds>0));
            Group(Command("money","Purchase","purchase",state==WishlistStatus.Planned && (active?.Goals is null || active.ActiveGoals.Any())),Command("arrow_down","Balance","balance",state==WishlistStatus.Purchased&&funds>0),Command("square","Close","close",state==WishlistStatus.Purchased&&funds==0));
        }
        else if (ribbonTab == "Home")
        {
            Group(Ui.IconButton("add", "Income", () => EditTransaction(TransactionKind.Income), theme.Success), Ui.IconButton("subtract", "Expense", () => EditTransaction(TransactionKind.Expense), theme.Error), Ui.IconButton("arrow_swap", "Transfer", () => EditTransaction(TransactionKind.Transfer), theme.Accent));
            editButton = Ui.IconButton("edit", "Edit", EditSelected, theme.Accent); deleteButton = Ui.IconButton("delete", "Delete", DeleteSelected, theme.Accent); Group(editButton, deleteButton);
            Group(Ui.IconButton("search", "Search", () => { page = "Transactions"; Render(); FocusSearch(); }, theme.Accent), Ui.IconButton("filter", "Filter", ShowFilters, theme.Accent));
        }
        else
        {
            var accent = theme.Accent;
            Group(Ui.IconButton("building_bank", "Accounts", () => Manage(true), accent), Ui.IconButton("tag", "Categories", () => Manage(false), accent), Ui.IconButton("calendar", "Period", SelectMonth, accent));
            Group(Ui.IconButton("target", "Purpose", ManagePurposes, accent));
            Group(Ui.IconButton("savings","Wishlist",ShowWishlist,accent),Ui.IconButton("money","Debt",ShowDebt,accent));
        }
        UIElement commandRow = groups;
        if (page == "Overview" && session.Document.Banks.Count > 0)
        {
            var summary = OverviewRibbonSummary(theme);
            var commands = new DockPanel();
            DockPanel.SetDock(groups, Dock.Left);
            DockPanel.SetDock(summary, Dock.Right);
            commands.Children.Add(groups);
            commands.Children.Add(summary);
            commands.LastChildFill = false;
            commandRow = new ScrollViewer
            {
                Content = commands, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
        }
        var border = new Border { Child = commandRow, Background = theme.Surface, BorderBrush = theme.Outline, BorderThickness = new Thickness(0,0,0,1) }; stack.Children.Add(border); return stack;
    }
    private UIElement BuildSidebar(WorkspaceTheme theme)
    {
        var s = Ui.Stack();
        var label = Ui.Text("Bank Accounts", 15, theme.Muted, bold: true); label.Margin = new Thickness(12); s.Children.Add(label);
        void BankButton(string name, Guid? id)
        {
            var active = selectedBank == id; var b = Ui.Button(name, () => { selectedBank = id; Render(); }); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12,8,12,8); b.Margin = new Thickness(0); b.FontSize = 12;
            theme.StyleButton(b, selected: active, square: true);
            b.Margin = new Thickness(0,0,0,4);
            b.Padding = new Thickness(20,8,12,8);
            b.FontSize = 14;
            b.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            b.ContentTemplate = (DataTemplate)FindResource("WorkspaceBankName");
            b.ToolTip = name;
            s.Children.Add(b);
        }
        BankButton("All accounts", null); foreach (var b in session.Document.Banks.OrderBy(b => b.Name)) BankButton(b.Name, b.Id);
        return new Border { Child = s, Background = theme.Sidebar, BorderBrush = theme.Outline, BorderThickness = new Thickness(0,0,1,0) };
    }
    private UIElement PageHeader(string heading, WorkspaceTheme theme)
    {
        var header = new DockPanel { Margin = new Thickness(24,2,24,2) };
        var period = Ui.Stack(Orientation.Horizontal);
        var prev = Ui.SquareIconButton("chevron_left", () => MoveMonth(-1), "Previous period", color: theme.Accent); period.Children.Add(prev);
        var label = periodMode == ReportPeriodMode.CalendarMonth ? month.ToString("MMMM yyyy", Display.Culture) : periodMode == ReportPeriodMode.PayCycle ? "Pay Cycle · " + month.ToString("MMM yyyy",Display.Culture) : "Custom Range";
        var monthButton = Ui.Button(label, SelectMonth); monthButton.MinWidth = 180; period.Children.Add(monthButton);
        var next = Ui.SquareIconButton("chevron_right", () => MoveMonth(1), "Next period", color: theme.Accent); period.Children.Add(next);
        monthButton.Height = prev.Height;
        monthButton.Padding = new Thickness(12, 0, 12, 0);
        monthButton.VerticalAlignment = VerticalAlignment.Center;
        foreach (Button button in period.Children) theme.StyleButton(button, outlined: true);
        AutomationProperties.SetName(prev, "Previous period");
        AutomationProperties.SetName(next, "Next period");
        var title = new DockPanel();
        var headingText = Ui.Text(heading, 20, theme.Text, bold: true);
        var range = SelectedRange;
        var dates = Ui.Text(Display.Date(range.Start) + " – " + Display.Date(range.End), 14, theme.Muted);
        dates.Margin = new Thickness(12, 0, 0, 0);
        dates.TextWrapping = TextWrapping.NoWrap;
        dates.TextTrimming = TextTrimming.CharacterEllipsis;
        dates.ToolTip = dates.Text;
        DockPanel.SetDock(headingText, Dock.Left);
        title.Children.Add(headingText);
        title.Children.Add(dates);
        DockPanel.SetDock(period, Dock.Right); header.Children.Add(period); header.Children.Add(title);
        return new Border { Child = header, BorderBrush = theme.Rule, BorderThickness = new Thickness(0,0,0,1) };
    }
    private void MoveMonth(int offset)
    {
        try
        {
            if (periodMode == ReportPeriodMode.CustomRange)
            {
                var days = (customEnd.DayNumber - customStart.DayNumber + 1) * offset;
                var start = customStart.AddDays(days); var end = customEnd.AddDays(days); customStart = start; customEnd = end;
            }
            else { var next = month.AddMonths(offset); new ReportPeriod(periodMode,next,customStart,customEnd).Range(session.Document.PaySchedule); month = next; }
            RememberPeriod(); Render();
        }
        catch (Exception e) when (e is ArgumentOutOfRangeException or InvalidDataException) { /* At the first/last supported period. */ }
    }
    private void SelectMonth()
    {
        var dialog = new PeriodDialog(this,session.Document,SelectedPeriod);
        if (dialog.ShowDialog() != true) return;
        if (!ReferenceEquals(dialog.Schedule,session.Document.PaySchedule) || dialog.Result != session.Document.SavedPeriod)
        { var copy = session.Document.Copy(); copy.PaySchedule = dialog.Schedule; copy.SavedPeriod = dialog.Result; session.Apply(copy); }
        periodMode = dialog.Result.Mode; month = dialog.Result.Month; customStart = dialog.Result.CustomStart; customEnd = dialog.Result.CustomEnd; Render();
    }
    private void RememberPeriod()
    {
        if (session.Document.SavedPeriod == SelectedPeriod) return;
        var copy = session.Document.Copy(); copy.SavedPeriod = SelectedPeriod; session.Apply(copy);
    }
    private void RestorePeriod(ReportPeriod period)
    { periodMode = period.Mode; month = period.Month; customStart = period.CustomStart; customEnd = period.CustomEnd; }
    private List<LedgerRow> Rows(bool filter)
    {
        var range = SelectedRange;
        IEnumerable<LedgerRow> rows = session.Document.Transactions.Where(t => range.Contains(t.Date) && (selectedBank is null || t.BankId == selectedBank || t.ToBankId == selectedBank))
            .OrderByDescending(t => t.Date).ThenByDescending(t => session.Document.Transactions.IndexOf(t)).SelectMany(t => LedgerRow.From(session.Document,t,Theme)).Where(r => !r.IsFee || selectedBank is null || r.Item.BankId == selectedBank);
        if (filter)
        {
            if (typeFilter != "All types") rows = rows.Where(r => r.Type == typeFilter);
            if (categoryFilter is Guid category) rows = rows.Where(r => category == FinanceDocument.AdminFeeFilterId ? r.IsFee : !r.IsFee && r.Item.CategoryId == category);
            if (purposeFilter is Guid purpose) rows = rows.Where(r => !r.IsFee && r.Item.Allocations?.Any(a => a.PurposeId == purpose) == true);
            if(debtTrackingFilter=="Tracked debts")rows=rows.Where(r=>session.Document.RelatedDebtIds(r.Item).Any());else if(debtTrackingFilter=="Not tracked")rows=rows.Where(r=>!session.Document.RelatedDebtIds(r.Item).Any());
            if (!string.IsNullOrWhiteSpace(search)) rows = rows.Where(LedgerRow.Search(search));
        }
        return rows.ToList();
    }
    private DataGrid MakeLedger()
    {
        // Star widths plus frozen columns can oscillate after a DPI round trip
        // when WPF rounds every measure to device pixels.
        var theme = Theme;
        var grid = new DataGrid
        {
            FrozenColumnCount = 1, UseLayoutRounding = false,
            Background = theme.Surface, RowBackground = theme.Surface, AlternatingRowBackground = theme.Surface,
            Foreground = theme.Text, BorderThickness = new Thickness(0), HorizontalGridLinesBrush = theme.Rule
        };
        var date = Ui.Column("Date", "Date", 1); date.SortMemberPath = "SortDate"; date.Width = 120; date.MinWidth = date.MaxWidth = 120;
        grid.Columns.Add(date); grid.Columns.Add(Ui.TypeColumn());
        var bank = Ui.Column("Bank Account", "Bank", 1.4); bank.MinWidth = 160; grid.Columns.Add(bank);
        var category = Ui.Column("Category", "Category", 1.1); category.MinWidth = 144; grid.Columns.Add(category);
        var description = Ui.Column("Description", "Description", 2.8); description.MinWidth = 220; grid.Columns.Add(description);
        grid.Columns.Add(Ui.AllocationColumn(theme.Accent, theme.Muted)); grid.Columns.Add(Ui.DebtColumn(theme.Accent, theme.Muted));
        var amount = Ui.Column("Amount","Amount",1.5,true); amount.MinWidth = 184; amount.SortMemberPath = "NumericAmount"; grid.Columns.Add(amount);
        foreach (var column in grid.Columns)
            column.HeaderStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));
        grid.SelectionChanged += (_, _) => { if (editButton is not null) editButton.IsEnabled = ledger?.SelectedItem is LedgerRow; if (deleteButton is not null) deleteButton.IsEnabled = ledger?.SelectedItem is LedgerRow; };
        grid.MouseDoubleClick += (_, e) => { var d = e.OriginalSource as DependencyObject; while (d is not null && d is not DataGridRow) d = VisualTreeHelper.GetParent(d); if (d is DataGridRow) EditSelected(); };
        return grid;
    }
    private UIElement TransactionsPage()
    {
        var theme = Theme;
        var outer = new DockPanel { Background = theme.Background }; var header = PageHeader("Transactions", theme); DockPanel.SetDock(header,Dock.Top); outer.Children.Add(header);
        var cardContent = new DockPanel();
        var filters = new DockPanel { Margin = new Thickness(0, 0, 0, 12) };
        var bankCombo = new ComboBox { Width = 190, DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All accounts") }.Concat(session.Document.Banks.OrderBy(b => b.Name).Select(b => new BankChoice(b.Id,b.Name))).ToList(), SelectedValuePath = "Id", SelectedValue = selectedBank, Margin = new Thickness(0,0,4,0) };
        bankCombo.Background = theme.Surface; bankCombo.Foreground = theme.Text; bankCombo.BorderBrush = theme.Outline;
        bankCombo.SelectedIndex = selectedBank is null ? 0 : session.Document.Banks.OrderBy(b => b.Name).ToList().FindIndex(b => b.Id == selectedBank) + 1;
        bankCombo.SelectionChanged += (_, _) => { selectedBank = (bankCombo.SelectedItem as BankChoice)?.Id; Render(); }; DockPanel.SetDock(bankCombo,Dock.Left); filters.Children.Add(bankCombo);
        var clear = theme.ActionButton("Clear Filters", () => { search = ""; typeFilter = "All types"; categoryFilter = null; purposeFilter = null; selectedBank = null; debtTrackingFilter="All transactions"; Render(); }); clear.Margin = new Thickness(4,0,0,0); DockPanel.SetDock(clear,Dock.Right); filters.Children.Add(clear);
        searchBox = new TextBox { Text = search, ToolTip = "Search transactions (Ctrl+F)", VerticalContentAlignment = VerticalAlignment.Center,
            Background = theme.Surface, Foreground = theme.Text, CaretBrush = theme.Text, SelectionBrush = theme.Accent };
        searchBox.SetCurrentValue(Control.BorderBrushProperty, theme.Outline);
        searchBox.TextChanged += (_, _) => { search = searchBox.Text; if (ledger is not null) Ui.RefreshLedger(ledger,Rows(true)); };
        filters.Children.Add(searchBox); DockPanel.SetDock(filters,Dock.Top); cardContent.Children.Add(filters);
        if (typeFilter != "All types" || categoryFilter is not null || purposeFilter is not null||debtTrackingFilter!="All transactions") { var note = theme.Label("Filter: " + typeFilter + (categoryFilter is Guid id ? " · " + session.Document.CategoryName(id) : "") + (purposeFilter is Guid purpose ? " · Purpose: " + session.Document.PurposeName(purpose) : "")+(debtTrackingFilter!="All transactions"?" · "+debtTrackingFilter:""),12,theme.Accent); note.Margin = new Thickness(0,0,0,8); DockPanel.SetDock(note,Dock.Top); cardContent.Children.Add(note); }
        var hint = theme.Label("Select a row to edit or delete. Admin Fee rows are linked to their original transaction.",12,theme.Muted); hint.Margin = new Thickness(0,10,0,0); DockPanel.SetDock(hint,Dock.Bottom); cardContent.Children.Add(hint);
        ledger = MakeLedger();
        ledger.ItemsSource = Rows(true);
        var surface = Ui.LedgerSurface(ledger, "No transactions to display", "Choose another period or adjust your search and filters.", theme.Text, theme.Muted);
        cardContent.Children.Add(surface);
        var card = theme.Card(cardContent, new Thickness(12));
        card.Margin = new Thickness(12);
        outer.Children.Add(card);
        return outer;
    }
    private void FocusSearch()
    {
        var target = searchBox;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input,new Action(() => {
            if (ReferenceEquals(searchBox,target) && target is { IsVisible:true }) target.Focus();
        }));
    }
    private sealed record BankChoice(Guid? Id,string Name);
    private void ShowFilters()
    {
        var dialog = new FormWindow(this,"Filter Transactions","Choose a type, category, purpose, or debt tracking filter. Bank and selected period filters also apply.","Apply",480);
        var s = Ui.Stack(); var types = new ComboBox { ItemsSource = new[] { "All types", "Income", "Expense", "Transfer" }, SelectedItem = typeFilter, Margin = new Thickness(0,8,0,16) }; s.Children.Add(Theme.Label("Type")); s.Children.Add(types);
        var cats = new ComboBox { DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All categories"), new BankChoice(FinanceDocument.AdminFeeFilterId,"Admin Fee (System)") }.Concat(session.Document.Categories.OrderBy(c => c.Kind).ThenBy(c => c.Name).Select(c => new BankChoice(c.Id,$"{c.Name} ({c.Kind})"))).ToList(), SelectedValuePath = "Id", SelectedValue = categoryFilter }; if (categoryFilter is null) cats.SelectedIndex = 0; s.Children.Add(Theme.Label("Category")); s.Children.Add(cats);
        var purposes = new ComboBox { DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All purposes") }.Concat(session.Document.Purposes.OrderBy(p => p.Name).Select(p => new BankChoice(p.Id,p.Name))).ToList(), SelectedValuePath = "Id", SelectedValue = purposeFilter, Margin = new Thickness(0,8,0,0) }; if (purposeFilter is null) purposes.SelectedIndex = 0; var purposeLabel = Theme.Label("Purpose"); purposeLabel.Margin = new Thickness(0,16,0,0); s.Children.Add(purposeLabel); s.Children.Add(purposes);
        var debtFilter=new ComboBox{ItemsSource=new[]{"All transactions","Tracked debts","Not tracked"},SelectedItem=debtTrackingFilter,Margin=new Thickness(0,8,0,0)};var debtLabel=Theme.Label("Debt tracking");debtLabel.Margin=new Thickness(0,16,0,0);s.Children.Add(debtLabel);s.Children.Add(debtFilter);
        dialog.AddFields(s); dialog.OnSubmit(() => { typeFilter = (string)types.SelectedItem; categoryFilter = (cats.SelectedItem as BankChoice)?.Id; purposeFilter = (purposes.SelectedItem as BankChoice)?.Id; debtTrackingFilter=(string)debtFilter.SelectedItem;page = "Transactions"; }); if (dialog.ShowDialog() == true) Render();
    }
    internal async Task<bool> InstallUpdateAsync(StagedUpdate package)
    {
        if (hasDocument && session.Dirty)
        {
            if (!ConfirmDialog.Confirm(this,"Save before updating","Save your changes before restarting to install the update.","Save and Update") || !Save()) return false;
        }
        IsEnabled = false; preparingUpdate = true;
        try { await PortableUpdater.StartAsync(package,hasDocument ? session.FilePath : null); }
        finally { IsEnabled = true; preparingUpdate = false; }
        updating = true; Close(); return true;
    }
    private bool CanLeave()
    {
        if (!hasDocument || !session.Dirty) return true;
        return ConfirmDialog.Unsaved(this,NameOfDocument + ".mpb") switch { "Save" => Save(), "Don't Save" => true, _ => false };
    }
    private void ResetView() { ResetWishlistViewport();ResetDebtView(); RestorePeriod(ReportPeriod.Restore(session.Document,DateOnly.FromDateTime(DateTime.Today))); selectedBank = null; categoryFilter = null; purposeFilter = null; search = ""; typeFilter = "All types"; debtTrackingFilter="All transactions";page = "Overview"; ribbonTab = "Home"; }
    private void NewDocument() { if (!CanLeave()) return; session.New(); hasDocument = true; ResetView(); Render(); }
    private void OpenDocument()
    {
        if (!CanLeave()) return;
        var picker = new OpenFileDialog { Filter = "Piggy documents (*.mpb)|*.mpb", Title = "Open Piggy Document", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        OpenPath(picker.FileName);
    }
    public void OpenPath(string path)
    {
        try { session.Open(path); hasDocument = true; ResetView(); var anchor = session.Document.Transactions.Count > 0 ? session.Document.Transactions.Max(t => t.Date) : DateOnly.FromDateTime(DateTime.Today); RestorePeriod(ReportPeriod.Restore(session.Document,anchor)); Render(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or ArgumentException or NotSupportedException) { ConfirmDialog.Alert(this,"Could not open document",e.Message); }
    }
    private bool Save(bool saveAs = false)
    {
        if (!hasDocument) return false;
        var path = session.FilePath;
        if (saveAs || path is null)
        {
            var picker = new SaveFileDialog { Filter = "Piggy documents (*.mpb)|*.mpb", DefaultExt = ".mpb", AddExtension = true, FileName = NameOfDocument + ".mpb", Title = saveAs ? "Save As" : "Save Piggy Document", OverwritePrompt = true };
            if (path is not null) picker.InitialDirectory = Path.GetDirectoryName(path);
            if (picker.ShowDialog(this) != true) return false; path = picker.FileName;
        }
        try { RememberPeriod(); session.Save(path); Render(); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or ArgumentException) { ConfirmDialog.Alert(this,"Could not save document",e.Message + "\n\nYour changes are still available in this window. Try Save As to another location."); return false; }
    }
    private void CloseDocument() { if (!CanLeave()) return; session.New(); hasDocument = false; ResetView(); Render(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (preparingUpdate || (!updating && !CanLeave())) e.Cancel = true; }
    private void Manage(bool banks)
    {
        new ManagerWindow(this,session,banks,UpdateTitle).ShowDialog();
        if (!session.Document.Banks.Any(b => b.Id == selectedBank)) selectedBank = null;
        if (categoryFilter != FinanceDocument.AdminFeeFilterId && !session.Document.Categories.Any(c => c.Id == categoryFilter)) categoryFilter = null; Render();
    }
    private void ManagePurposes()
    {
        new PurposeManagerWindow(this,session,UpdateTitle).ShowDialog();
        if (!session.Document.Purposes.Any(p => p.Id == purposeFilter)) purposeFilter = null;
        Render();
    }
    private void AddFirstBank() { var dialog = new BankDialog(this,session.Document); if (dialog.ShowDialog() == true) { var copy = session.Document.Copy(); copy.Banks.Add(dialog.Result!); session.Apply(copy); } Render(); }
    private void EditTransaction(TransactionKind kind, Transaction? existing = null)
    {
        if(existing is not null && RedirectWishlistTransaction(existing))return;
        if (session.Document.Banks.Count < (kind == TransactionKind.Transfer ? 2 : 1)) { ConfirmDialog.Alert(this,"Add a bank first", kind == TransactionKind.Transfer ? "A transfer needs two different banks. Add your banks under Data → Bank Accounts." : "Add your first bank account to start recording transactions."); Manage(true); return; }
        var added = new TransactionDialog(this,session,kind,existing).ShowDialog() == true;
        if (added) { var item = existing is null ? session.Document.Transactions.Last() : session.Document.Transactions.Single(t => t.Id == existing.Id); if (!SelectedRange.Contains(item.Date)) RestorePeriod(SelectedPeriod.AfterTransaction(item.Date)); RememberPeriod(); page = "Transactions"; search = ""; typeFilter = "All types"; categoryFilter = null; purposeFilter = null; selectedBank = null; debtTrackingFilter="All transactions"; }
        Render();
    }
    private void EditSelected() { if (ledger?.SelectedItem is LedgerRow row) EditTransaction(row.Item.Kind,row.Item); }
    private void DeleteSelected()
    {
        if (ledger?.SelectedItem is not LedgerRow row) return;
        if(RedirectWishlistTransaction(row.Item,true))return;
        var transfer = row.Item.Kind == TransactionKind.Transfer;
        if (!ConfirmDialog.Confirm(this, transfer ? "Delete transfer?" : "Delete transaction?", transfer ? "This removes the transfer from both banks and deletes its linked fee, if any." : "This removes the transaction and recalculates the bank balance.","Delete")) return;
        var copy = session.Document.Copy(); copy.Transactions.RemoveAll(t => t.Id == row.Item.Id);
        try { copy.Validate(); } catch(InvalidDataException e){ConfirmDialog.Alert(this,"Could not delete transaction",e.Message);return;}
        var banks = new[] { row.Item.BankId, row.Item.ToBankId ?? row.Item.BankId }.Distinct().Where(copy.HasNegativeHistory).ToList();
        if (banks.Count > 0 && !ConfirmDialog.Confirm(this,"Negative balance","Deleting this transaction leaves a negative balance in the recorded history of: " + string.Join(", ",banks.Select(copy.BankName)) + ". Continue?","Continue")) return;
        try { session.Apply(copy); Render(); } catch(InvalidDataException e){ConfirmDialog.Alert(this,"Could not delete transaction",e.Message);}
    }
}

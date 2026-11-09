using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow : Window
{
    private readonly DocumentSession session = new();
    private readonly DockPanel root = new();
    private readonly ContentControl body = new();
    private bool fittingWindow;
    private readonly UpdateSession updates = new();
    private TextBlock status = Ui.Text("", 12, Ui.Muted);
    private bool hasDocument, backstage, updating, preparingUpdate;
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
        // Keep the workspace usable when a high-DPI monitor has less logical space.
        body.MinWidth = 960; body.MinHeight = 560;
        Content = root; root.Children.Add(new ScrollViewer { Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
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
        InputBindings.Add(new KeyBinding(ApplicationCommands.SaveAs, Key.S, ModifierKeys.Control | ModifierKeys.Shift));
        Bind(ApplicationCommands.Find, () => { page = "Transactions"; backstage = false; Render(); FocusSearch(); }, () => hasDocument);
        Closing += OnClosing;
        Closed += (_,_) => updates.Dispose();
        Render();
    }
    private void UpdateTitle()
    {
        Title = hasDocument ? $"{NameOfDocument}{(session.Dirty ? "*" : "")} — Piggy" : "Piggy";
        status.Text = hasDocument ? $"{session.Document.Transactions.Count} transactions   |   {(session.Dirty ? "Unsaved changes" : session.FilePath is null ? "Not saved yet" : "Saved")} {(session.FilePath is not null ? "  |   " + session.FilePath : "")}" : "";
        status.ToolTip = hasDocument && session.FilePath is string path
            ? new TextBlock { Text = path, TextWrapping = TextWrapping.Wrap, MaxWidth = 600 } : null;
        ToolTipService.SetShowDuration(status,60000);
    }
    private void Render()
    {
        CaptureWishlistViewport();
        CaptureDebtViewport();
        status = Ui.Text("", 12, Ui.Muted);
        UpdateTitle(); ledger = null; editButton = null; deleteButton = null; searchBox = null;
        if (!hasDocument) { body.Content = StartPage(); return; }
        if (backstage) { body.Content = FilePage(); return; }
        if(page=="Wishlist"){
            var visible=session.Document.Wishlists.Where(w=>selectedBank is null||session.Document.WishlistAllocated(w.Id,selectedBank)>0).ToList();
            if(!visible.Any(w=>w.Id==selectedWishlist))selectedWishlist=visible.FirstOrDefault()?.Id;
        }
        if(page=="Debt"&&!VisibleDebts().Any(d=>d.Id==selectedDebt))selectedDebt=VisibleDebts().FirstOrDefault()?.Id;
        var layout = new DockPanel { Background = Ui.Brush("#F3F3F6") };
        var ribbon = BuildRibbon(); DockPanel.SetDock(ribbon, Dock.Top); layout.Children.Add(ribbon);
        status.TextWrapping = TextWrapping.NoWrap; status.TextTrimming = TextTrimming.CharacterEllipsis;
        var footerContent = new DockPanel(); var indicator = new UpdateIndicator(this,updates); DockPanel.SetDock(indicator,Dock.Right); footerContent.Children.Add(indicator); footerContent.Children.Add(status);
        var footer = new Border { Height = 40, BorderBrush = Ui.Line, BorderThickness = new Thickness(0,1,0,0), Background = Ui.Brush("#F8F8FA"), Padding = new Thickness(12,0,12,0), Child = footerContent }; DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var tabs = Ui.Stack(Orientation.Horizontal); tabs.Background = Ui.Brush("#F5F5F8");
        foreach (var label in new[] { "Overview", "Transactions", "Wishlist", "Debt" }) { var name = label; var b = Ui.Button(label, () => { page = name; Render(); }); b.Margin = new Thickness(0); b.Padding = new Thickness(20,8,20,8); b.FontSize = 12; Ui.SelectNavigation(b,page == name); tabs.Children.Add(b); }
        DockPanel.SetDock(tabs, Dock.Bottom); layout.Children.Add(tabs);
        var side = BuildSidebar(); DockPanel.SetDock(side, Dock.Left); layout.Children.Add(side);
        var workspace = page == "Overview" ? Overview() : page == "Wishlist" ? WishlistPage() : page=="Debt"?DebtPage():TransactionsPage(); layout.Children.Add(workspace); body.Content = layout;
        if (editButton is not null) editButton.IsEnabled = false; if (deleteButton is not null) deleteButton.IsEnabled = false;
    }
    private UIElement StartPage()
    {
        var grid = new Grid { Background = Brushes.White };
        var actions = Ui.Stack(); actions.HorizontalAlignment = HorizontalAlignment.Center; actions.VerticalAlignment = VerticalAlignment.Center; actions.Margin = new Thickness(24);
        var logo = new Image { Source = Ui.BrandImage("Brand/piggy-logo.png"), Width = 320, Height = 320, Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(logo, BitmapScalingMode.HighQuality); AutomationProperties.SetName(logo,"Piggy logo"); actions.Children.Add(logo);
        foreach (var (label, action) in new (string,Action)[] { ("New",NewDocument), ("Open",OpenDocument) })
        {
            var button = Ui.Button(label,action,label == "New"); button.Width = 240; button.Height = 44; button.FontSize = 16;
            button.Margin = new Thickness(0,0,0,label == "New" ? 12 : 0); actions.Children.Add(button);
        }
        grid.Children.Add(actions); AddUpdateCorner(grid); return grid;
    }
    private void AddUpdateCorner(Grid grid)
    {
        var indicator = new UpdateIndicator(this,updates) { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(24) };
        Grid.SetColumn(indicator,Math.Max(0,grid.ColumnDefinitions.Count-1)); grid.Children.Add(indicator);
    }
    private UIElement BuildRibbon()
    {
        var stack = Ui.Stack(); stack.Background = Ui.Brush("#F5F4F7"); var tabs = Ui.Stack(Orientation.Horizontal);
        foreach (var label in new[] { "File", "Home", "Data", "View" }) { var name = label; var b = Ui.Button(label, () => { if (name == "File") backstage = true; else ribbonTab = name; Render(); }); b.Margin = new Thickness(0); b.Padding = new Thickness(20,0,20,0); b.Height = 32; b.MinHeight = 0; b.FontSize = 12; Ui.SelectNavigation(b,ribbonTab == name); tabs.Children.Add(b); }
        var tabRow = new DockPanel { Height = 32 };
        var save = Ui.SquareIconButton("save", () => Save(), "Save (Ctrl+S)", 32, Ui.Accent);
        save.Margin = new Thickness(0,0,4,0); AutomationProperties.SetName(save,"Save");
        DockPanel.SetDock(save,Dock.Right); tabRow.Children.Add(save); tabRow.Children.Add(tabs); stack.Children.Add(tabRow);
        var groups = Ui.Stack(Orientation.Horizontal);
        void Group(string label, params Button[] buttons)
        {
            var s = Ui.Stack(); var row = Ui.Stack(Orientation.Horizontal);
            foreach (var b in buttons)
            {
                b.Margin = new Thickness(0); b.MinWidth = 0; b.Width = b.Height = 72; b.Padding = new Thickness(4);
                b.HorizontalContentAlignment = HorizontalAlignment.Center; b.VerticalContentAlignment = VerticalAlignment.Center;
                if (b.Content is StackPanel command)
                {
                    command.Width = 64; command.HorizontalAlignment = HorizontalAlignment.Center; command.VerticalAlignment = VerticalAlignment.Center;
                    if (command.Children.OfType<Image>().FirstOrDefault() is Image icon) { icon.Width = icon.Height = 32; icon.HorizontalAlignment = HorizontalAlignment.Center; icon.Margin = new Thickness(0,0,0,4); }
                    if (command.Children.OfType<TextBlock>().FirstOrDefault() is TextBlock labelText)
                    {
                        labelText.FontSize = 8.0 * 96 / 72; labelText.Width = 64; labelText.Height = 16;
                        labelText.Margin = new Thickness(0); labelText.TextWrapping = TextWrapping.NoWrap; labelText.TextAlignment = TextAlignment.Center;
                        labelText.TextTrimming = TextTrimming.CharacterEllipsis; b.ToolTip = labelText.Text; AutomationProperties.SetName(b,labelText.Text);
                    }
                }
                row.Children.Add(b);
            }
            s.Children.Add(row); var text = Ui.Text(label,11,Ui.Muted); text.HorizontalAlignment = HorizontalAlignment.Center;
            text.Margin = new Thickness(0,4,0,0); s.Children.Add(text);
            groups.Children.Add(new Border { Child = s, BorderBrush = Ui.Line, BorderThickness = new Thickness(0,0,1,0), Padding = new Thickness(4) });
        }
        if(ribbonTab=="Home"&&page=="Debt")
        {
            var active=ActiveDebt;var remaining=active is null?0:session.Document.DebtAmounts(active.Id).Remaining;
            Button Command(string icon,string label,string action,bool enabled=true){var b=Ui.IconButton(icon,label,()=>DebtAction(action),Ui.Accent);b.IsEnabled=enabled;return b;}
            Group("Debt",Command("add","New Debt","new"),Command("edit","Edit Details","edit",active is not null),Command("delete","Remove","remove",active is not null));
            Group("Transactions",Command("arrow_up",active?.Kind==DebtKind.Self?"Add Debt":"Borrow","borrow",active is {IsPaid:false}),Command("arrow_down","Payment","payment",active is {IsPaid:false}&&remaining>0));
            Group("Settlement",Command("square","Mark Paid","paid",active is {IsPaid:false}&&remaining==0),Command("arrow_sync","Reopen","reopen",active is {IsPaid:true}));
        }
        else if (ribbonTab == "Home" && page == "Wishlist")
        {
            var active=ActiveWishlist;var state=active is null?WishlistStatus.Cancelled:session.Document.WishlistState(active.Id);var funds=active is null?0:session.Document.WishlistAllocated(active.Id);
            Button Command(string icon,string text,string action,bool enabled=true){var b=Ui.IconButton(icon,text,()=>WishlistAction(action),Ui.Accent);b.IsEnabled=enabled;return b;}
            Group("Wishlist",Command("add","New Wishlist","new"),Command("edit","Edit","edit",state==WishlistStatus.Planned),Command("dismiss","Cancel","cancel",state==WishlistStatus.Planned));
            Group("Funds",Command("add","Allocate","allocate",state==WishlistStatus.Planned),Command("arrow_swap","Move","move",state==WishlistStatus.Planned&&funds>0),Command("subtract","Release","release",state==WishlistStatus.Planned&&funds>0));
            Group("Purchase",Command("money","Purchase","purchase",state==WishlistStatus.Planned),Command("arrow_down","Balance","balance",state==WishlistStatus.Purchased&&funds>0),Command("square","Close","close",state==WishlistStatus.Purchased&&funds==0));
        }
        else if (ribbonTab == "Home")
        {
            Group("Transactions", Ui.IconButton("add", "Income", () => EditTransaction(TransactionKind.Income), Ui.Green), Ui.IconButton("subtract", "Expense", () => EditTransaction(TransactionKind.Expense), Ui.Red), Ui.IconButton("arrow_swap", "Transfer", () => EditTransaction(TransactionKind.Transfer), Ui.Accent));
            editButton = Ui.IconButton("edit", "Edit", EditSelected, Ui.Brush("#2167B1")); deleteButton = Ui.IconButton("delete", "Delete", DeleteSelected, Ui.Accent); Group("Edit", editButton, deleteButton);
            Group("Find", Ui.IconButton("search", "Search", () => { page = "Transactions"; Render(); FocusSearch(); }, Ui.Brush("#2167B1")), Ui.IconButton("filter", "Filter", ShowFilters));
        }
        else if (ribbonTab == "Data")
        {
            Group("Manage", Ui.IconButton("building_bank", "Accounts", () => Manage(true), Ui.Accent), Ui.IconButton("tag", "Categories", () => Manage(false), Ui.Accent), Ui.IconButton("calendar", "Period", SelectMonth, Ui.Accent));
            Group("Allocation", Ui.IconButton("target", "Purpose", ManagePurposes, Ui.Accent));
            Group("Planning",Ui.IconButton("savings","Wishlist",ShowWishlist,Ui.Accent),Ui.IconButton("money","Debt",ShowDebt,Ui.Accent));
        }
        else Group("Workspace", Ui.IconButton("money", "Overview", () => { page = "Overview"; Render(); }, Ui.Accent), Ui.IconButton("document", "Transactions", () => { page = "Transactions"; Render(); }, Ui.Accent),Ui.IconButton("savings","Wishlist",ShowWishlist,Ui.Accent),Ui.IconButton("money","Debt",ShowDebt,Ui.Accent));
        var border = new Border { Child = groups, Background = Brushes.White, BorderBrush = Ui.Line, BorderThickness = new Thickness(0,0,0,1) }; stack.Children.Add(border); return stack;
    }
    private UIElement BuildSidebar()
    {
        var s = Ui.Stack(); s.Width = 200; s.Margin = new Thickness(0);
        var label = Ui.Text("Bank Accounts", 15, bold: true); label.Margin = new Thickness(12); s.Children.Add(label);
        void BankButton(string name, Guid? id)
        {
            var active = selectedBank == id; var b = Ui.Button(name, () => { selectedBank = id; Render(); }); b.HorizontalContentAlignment = HorizontalAlignment.Left; b.Padding = new Thickness(12,8,12,8); b.Margin = new Thickness(0); b.FontSize = 12; Ui.SelectNavigation(b,active); s.Children.Add(b);
        }
        BankButton("All accounts", null); foreach (var b in session.Document.Banks.OrderBy(b => b.Name)) BankButton(b.Name, b.Id);
        var add = Ui.Button("Manage Banks", () => Manage(true)); add.Margin = new Thickness(12,16,12,0); add.FontSize = 12; add.Foreground = Ui.Accent; s.Children.Add(add);
        return new Border { Child = s, Background = Ui.Brush("#FBFBFC"), BorderBrush = Ui.Line, BorderThickness = new Thickness(0,0,1,0) };
    }
    private UIElement PageHeader(string heading)
    {
        var header = new DockPanel { Margin = new Thickness(12,8,12,8) };
        var period = Ui.Stack(Orientation.Horizontal);
        var prev = Ui.SquareIconButton("chevron_left", () => MoveMonth(-1), "Previous period"); period.Children.Add(prev);
        var label = periodMode == ReportPeriodMode.CalendarMonth ? month.ToString("MMMM yyyy", Display.Culture) : periodMode == ReportPeriodMode.PayCycle ? "Pay Cycle · " + month.ToString("MMM yyyy",Display.Culture) : "Custom Range";
        var monthButton = Ui.Button(label, SelectMonth); monthButton.MinWidth = 180; period.Children.Add(monthButton);
        var next = Ui.SquareIconButton("chevron_right", () => MoveMonth(1), "Next period"); period.Children.Add(next);
        var title = Ui.Stack(); title.Children.Add(Ui.Text(heading,24,bold:true)); var range = SelectedRange; title.Children.Add(Ui.Text(Display.Date(range.Start) + " – " + Display.Date(range.End),12,Ui.Muted));
        DockPanel.SetDock(period, Dock.Right); header.Children.Add(period); header.Children.Add(title); return new Border { Child = header, BorderBrush = Ui.Line, BorderThickness = new Thickness(0,0,0,1) };
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
    private UIElement Overview()
    {
        var outer = new DockPanel { Margin = new Thickness(0), Background = Brushes.White }; var header = PageHeader("Overview"); DockPanel.SetDock(header, Dock.Top); outer.Children.Add(header);
        var stack = Ui.Stack();
        if (session.Document.Banks.Count == 0)
        {
            var empty = Ui.Stack(); empty.Children.Add(Ui.Text("Add your first bank", 24, bold:true)); var help = Ui.Text("Set an opening date and balance, then start recording transactions.", 15, Ui.Muted); help.Margin = new Thickness(0,12,0,20); empty.Children.Add(help); var button = Ui.Button("Add Bank", () => AddFirstBank(), true); button.HorizontalAlignment = HorizontalAlignment.Left; empty.Children.Add(button); stack.Children.Add(Ui.Box(empty, new Thickness(28)));
        }
        else
        {
            var top = new Grid(); top.ColumnDefinitions.Add(new ColumnDefinition()); top.ColumnDefinitions.Add(new ColumnDefinition());
            var balancePanel = Ui.Stack(); balancePanel.Margin = new Thickness(0,0,16,0); balancePanel.Children.Add(Ui.Text(selectedBank is null ? "Total Balance" : session.Document.BankName(selectedBank.Value) + " Balance", 18, bold:true));
            var today = DateOnly.FromDateTime(DateTime.Today); var banks = session.Document.Banks.Where(b => selectedBank is null || b.Id == selectedBank).ToList();
            var total = banks.Sum(b => session.Document.Balance(b.Id, today)); var value = Ui.Text(Display.Money(total), 28, total < 0 ? Ui.Red : Ui.Accent, true); value.Margin = new Thickness(0,12,0,4); balancePanel.Children.Add(value); balancePanel.Children.Add(Ui.Text("Current balance as of " + Display.Date(today), 12, Ui.Muted));
            var reserved=banks.Sum(b=>session.Document.WishlistReserved(b.Id,today));
            var allocated=Ui.Text("Wishlist Allocation: "+Display.Money(reserved),14,Ui.Muted);allocated.Margin=new Thickness(0,8,0,0);balancePanel.Children.Add(allocated);
            balancePanel.Children.Add(Ui.Text("Available Balance: "+Display.Money(total-reserved),16,bold:true));
            foreach (var bank in banks) { var row = new DockPanel { Margin = new Thickness(0,12,0,0) }; var amount = Ui.Text(Display.Money(session.Document.Balance(bank.Id, today)), 14); DockPanel.SetDock(amount,Dock.Right); row.Children.Add(amount); row.Children.Add(Ui.Text(bank.Name,14)); balancePanel.Children.Add(row); }
            top.Children.Add(balancePanel);
            var summary = Ui.Stack(); summary.Margin = new Thickness(16,0,0,0); summary.Children.Add(Ui.Text(periodMode == ReportPeriodMode.CalendarMonth ? month.ToString("MMMM yyyy", Display.Culture) : periodMode == ReportPeriodMode.PayCycle ? "Pay Cycle" : "Custom Range",18,bold:true));
            var range = SelectedRange; if (periodMode != ReportPeriodMode.CalendarMonth) summary.Children.Add(Ui.Text(Display.Date(range.Start) + " – " + Display.Date(range.End),12,Ui.Muted));
            var totals = session.Document.Totals(range.Start, range.End, selectedBank);
            foreach (var (label, amount, color) in new[] { ("Income", totals.Income, Ui.Green), ("Expenses", totals.Expense, Ui.Red), ("Net Income", totals.Income - totals.Expense, Ui.Ink) })
            { var row = new DockPanel { Margin = new Thickness(0,16,0,0) }; var money = Ui.Text(Display.Money(amount),16,color,true); DockPanel.SetDock(money,Dock.Right); row.Children.Add(money); row.Children.Add(Ui.Text(label,14)); summary.Children.Add(row); }
            var summaryBorder = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1,0,0,0), Child = summary }; Grid.SetColumn(summaryBorder,1); top.Children.Add(summaryBorder); stack.Children.Add(new Border { Child = top, Padding = new Thickness(12), BorderBrush = Ui.Line, BorderThickness = new Thickness(0,0,0,1) });
            var recent = Ui.Stack(); recent.Margin = new Thickness(0); var recentTitle = Ui.Text("Recent Transactions",18,bold:true); recentTitle.Margin = new Thickness(12); recent.Children.Add(recentTitle);
            var grid = MakeLedger(false); var recentRows = Rows(false).Take(5).ToList(); grid.ItemsSource = recentRows; grid.Height = recentRows.Count == 0 ? 160 : 34 + 32 * recentRows.Count;
            recent.Children.Add(Ui.LedgerSurface(grid,"No transactions in this period","Choose another period or add an Income, Expense, or Transfer.")); stack.Children.Add(recent);
        }
        outer.Children.Add(new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); return outer;
    }
    private List<LedgerRow> Rows(bool filter)
    {
        var range = SelectedRange;
        IEnumerable<LedgerRow> rows = session.Document.Transactions.Where(t => range.Contains(t.Date) && (selectedBank is null || t.BankId == selectedBank || t.ToBankId == selectedBank))
            .OrderByDescending(t => t.Date).ThenByDescending(t => session.Document.Transactions.IndexOf(t)).SelectMany(t => LedgerRow.From(session.Document,t)).Where(r => !r.IsFee || selectedBank is null || r.Item.BankId == selectedBank);
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
    private DataGrid MakeLedger(bool full)
    {
        var grid = new DataGrid { Margin = new Thickness(12,0,12,0) }; grid.Columns.Add(new DataGridTextColumn { Header = "Date", Binding = new System.Windows.Data.Binding("Date"), SortMemberPath = "SortDate", Width = 120 }); grid.Columns.Add(Ui.TypeColumn());
        grid.Columns.Add(Ui.Column("Bank Account","Bank",1.2)); if (full) grid.Columns.Add(Ui.Column("Category","Category",1)); grid.Columns.Add(Ui.Column("Description","Description",2.4));
        if (full)
        {
            grid.Columns.Add(Ui.AllocationColumn()); grid.Columns.Add(Ui.DebtColumn());
        }
        var amount = Ui.Column("Amount","Amount",1.2,true); amount.SortMemberPath = "NumericAmount"; grid.Columns.Add(amount);
        grid.SelectionChanged += (_, _) => { if (editButton is not null) editButton.IsEnabled = ledger?.SelectedItem is LedgerRow; if (deleteButton is not null) deleteButton.IsEnabled = ledger?.SelectedItem is LedgerRow; };
        if (full) grid.MouseDoubleClick += (_, e) => { var d = e.OriginalSource as DependencyObject; while (d is not null && d is not DataGridRow) d = VisualTreeHelper.GetParent(d); if (d is DataGridRow) EditSelected(); };
        return grid;
    }
    private UIElement TransactionsPage()
    {
        var outer = new DockPanel { Margin = new Thickness(0), Background = Brushes.White }; var header = PageHeader("Transactions"); DockPanel.SetDock(header,Dock.Top); outer.Children.Add(header);
        var filters = new DockPanel { Margin = new Thickness(12,8,12,8) };
        var bankCombo = new ComboBox { Width = 190, DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All accounts") }.Concat(session.Document.Banks.OrderBy(b => b.Name).Select(b => new BankChoice(b.Id,b.Name))).ToList(), SelectedValuePath = "Id", SelectedValue = selectedBank, Margin = new Thickness(0,0,4,0) };
        bankCombo.SelectedIndex = selectedBank is null ? 0 : session.Document.Banks.OrderBy(b => b.Name).ToList().FindIndex(b => b.Id == selectedBank) + 1;
        bankCombo.SelectionChanged += (_, _) => { selectedBank = (bankCombo.SelectedItem as BankChoice)?.Id; Render(); }; DockPanel.SetDock(bankCombo,Dock.Left); filters.Children.Add(bankCombo);
        var clear = Ui.Button("Clear Filters", () => { search = ""; typeFilter = "All types"; categoryFilter = null; purposeFilter = null; selectedBank = null; debtTrackingFilter="All transactions"; Render(); }); clear.Margin = new Thickness(4,0,0,0); DockPanel.SetDock(clear,Dock.Right); filters.Children.Add(clear);
        searchBox = new TextBox { Text = search, ToolTip = "Search transactions (Ctrl+F)", VerticalContentAlignment = VerticalAlignment.Center };
        searchBox.TextChanged += (_, _) => { search = searchBox.Text; if (ledger is not null) Ui.RefreshLedger(ledger,Rows(true)); };
        filters.Children.Add(searchBox); DockPanel.SetDock(filters,Dock.Top); outer.Children.Add(filters);
        if (typeFilter != "All types" || categoryFilter is not null || purposeFilter is not null||debtTrackingFilter!="All transactions") { var note = Ui.Text("Filter: " + typeFilter + (categoryFilter is Guid id ? " · " + session.Document.CategoryName(id) : "") + (purposeFilter is Guid purpose ? " · Purpose: " + session.Document.PurposeName(purpose) : "")+(debtTrackingFilter!="All transactions"?" · "+debtTrackingFilter:""),12,Ui.Accent); note.Margin = new Thickness(0,0,0,12); DockPanel.SetDock(note,Dock.Top); outer.Children.Add(note); }
        var hint = Ui.Text("Select a row to edit or delete. Admin Fee rows are linked to their original transaction.",12,Ui.Muted); hint.Margin = new Thickness(12,8,12,8); DockPanel.SetDock(hint,Dock.Bottom); outer.Children.Add(hint);
        ledger = MakeLedger(true); ledger.ItemsSource = Rows(true); outer.Children.Add(Ui.LedgerSurface(ledger,"No transactions to display","Choose another period or adjust your search and filters.")); return outer;
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
        var s = Ui.Stack(); var types = new ComboBox { ItemsSource = new[] { "All types", "Income", "Expense", "Transfer" }, SelectedItem = typeFilter, Margin = new Thickness(0,8,0,16) }; s.Children.Add(Ui.Text("Type")); s.Children.Add(types);
        var cats = new ComboBox { DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All categories"), new BankChoice(FinanceDocument.AdminFeeFilterId,"Admin Fee (System)") }.Concat(session.Document.Categories.OrderBy(c => c.Kind).ThenBy(c => c.Name).Select(c => new BankChoice(c.Id,$"{c.Name} ({c.Kind})"))).ToList(), SelectedValuePath = "Id", SelectedValue = categoryFilter }; if (categoryFilter is null) cats.SelectedIndex = 0; s.Children.Add(Ui.Text("Category")); s.Children.Add(cats);
        var purposes = new ComboBox { DisplayMemberPath = "Name", ItemsSource = new[] { new BankChoice(null,"All purposes") }.Concat(session.Document.Purposes.OrderBy(p => p.Name).Select(p => new BankChoice(p.Id,p.Name))).ToList(), SelectedValuePath = "Id", SelectedValue = purposeFilter, Margin = new Thickness(0,8,0,0) }; if (purposeFilter is null) purposes.SelectedIndex = 0; var purposeLabel = Ui.Text("Purpose"); purposeLabel.Margin = new Thickness(0,16,0,0); s.Children.Add(purposeLabel); s.Children.Add(purposes);
        var debtFilter=new ComboBox{ItemsSource=new[]{"All transactions","Tracked debts","Not tracked"},SelectedItem=debtTrackingFilter,Margin=new Thickness(0,8,0,0)};var debtLabel=Ui.Text("Debt tracking");debtLabel.Margin=new Thickness(0,16,0,0);s.Children.Add(debtLabel);s.Children.Add(debtFilter);
        dialog.AddFields(s); dialog.OnSubmit(() => { typeFilter = (string)types.SelectedItem; categoryFilter = (cats.SelectedItem as BankChoice)?.Id; purposeFilter = (purposes.SelectedItem as BankChoice)?.Id; debtTrackingFilter=(string)debtFilter.SelectedItem;page = "Transactions"; }); if (dialog.ShowDialog() == true) Render();
    }
    private UIElement FilePage()
    {
        var grid = new Grid(); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(184) }); grid.ColumnDefinitions.Add(new ColumnDefinition());
        var nav = Ui.Stack(); nav.Background = Ui.Accent; nav.Margin = new Thickness(0);
        var back = Ui.IconButton("arrow_left","Back",() => { backstage = false; Render(); },Brushes.White,false); back.Style = (Style)FindResource("BackstageButton"); back.Content = BackLabel(); back.Padding = new Thickness(16); back.Margin = new Thickness(0,8,0,16); nav.Children.Add(back);
        foreach (var (label,icon,action) in new (string,string,Action)[] { ("New","document",NewDocument),("Open","folder_open",OpenDocument),("Save","save",() => Save()),("Save As","save",() => Save(true)),("Close","folder_open",CloseDocument) })
        { var b = Ui.IconButton(icon,label,action,Brushes.White,false); b.Style = (Style)FindResource("BackstageButton"); b.Padding = new Thickness(16,12,16,12); b.Margin = new Thickness(0); var s = (StackPanel)b.Content; ((TextBlock)s.Children[1]).Foreground = Brushes.White; ((TextBlock)s.Children[1]).FontSize = 12; b.Foreground = Brushes.White; nav.Children.Add(b); } grid.Children.Add(nav);
        var info = Ui.Stack(); info.Margin = new Thickness(32); info.Children.Add(Ui.Text("Save",32,bold:true)); var intro = Ui.Text("Save changes to your current file.",16,Ui.Muted); intro.Margin = new Thickness(0,12,0,32); info.Children.Add(intro);
        var documentRow = new DockPanel(); var documentIcon = Ui.Icon("document",64,Ui.Ink); documentIcon.VerticalAlignment = VerticalAlignment.Top; documentIcon.Margin = new Thickness(0,0,28,0); DockPanel.SetDock(documentIcon,Dock.Left); documentRow.Children.Add(documentIcon);
        var details = Ui.Stack(); var filename = Ui.Text(NameOfDocument + ".mpb",20,bold:true); filename.Margin = new Thickness(0,0,0,16); details.Children.Add(filename); details.Children.Add(Ui.Text("Location:",14,Ui.Muted)); details.Children.Add(Ui.Text(session.FilePath ?? "Choose a name and location when you first save.",15,Ui.Muted));
        var note = Ui.Text(session.Dirty ? "Unsaved changes" : session.FilePath is null ? "Not saved yet" : "All changes saved",14,Ui.Muted); note.Margin = new Thickness(0,16,0,24); details.Children.Add(note); var save = Ui.Button("Save",() => Save(),true); save.Width = 180; save.HorizontalAlignment = HorizontalAlignment.Left; details.Children.Add(save); documentRow.Children.Add(details); info.Children.Add(documentRow);
        Grid.SetColumn(info,1); grid.Children.Add(new ScrollViewer { Content = info, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0,0,0,64) }.AtColumn(1)); AddUpdateCorner(grid); return grid;
    }
    private UIElement BackLabel() { var s = Ui.Stack(Orientation.Horizontal); s.Children.Add(Ui.Icon("arrow_left",24,Brushes.White)); var text = Ui.Text("Back",12,Brushes.White); text.Margin = new Thickness(12,0,0,0); s.Children.Add(text); return s; }
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
    private void ResetView() { ResetWishlistViewport();ResetDebtView(); RestorePeriod(ReportPeriod.Restore(session.Document,DateOnly.FromDateTime(DateTime.Today))); selectedBank = null; categoryFilter = null; purposeFilter = null; search = ""; typeFilter = "All types"; debtTrackingFilter="All transactions";page = "Overview"; ribbonTab = "Home"; backstage = false; }
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

internal static class LayoutExtensions
{
    public static T AtColumn<T>(this T element, int column) where T : UIElement { Grid.SetColumn(element,column); return element; }
}

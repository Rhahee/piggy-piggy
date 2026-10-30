using System.Windows;
using System.IO;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public class FormWindow : Window
{
    protected readonly StackPanel Fields = Ui.Stack();
    protected readonly TextBlock Error = Ui.Text("", 13, Ui.Red);
    protected readonly Button Submit;
    public FormWindow(Window owner, string title, string subtitle, string buttonText, double width = 520)
    {
        Ui.ApplyWindowStyle(this);
        Owner = owner; Title = title + " — Piggy"; Width = width; SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(500, SystemParameters.WorkArea.Height - 48); ResizeMode = ResizeMode.CanResize;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; MinWidth = 440;
        void KeepFormInWorkArea()
        {
            if(!IsLoaded||WindowState!=WindowState.Normal)return;
            var area=WindowBounds.WorkArea(this);MaxHeight=Math.Max(320,area.Height-48);
            Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-ActualHeight));
        }
        Loaded+=(_,_)=>KeepFormInWorkArea();SizeChanged+=(_,_)=>KeepFormInWorkArea();
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var header = Ui.Stack();
        var heading = Ui.Stack(Orientation.Horizontal);
        var kind = title.Split(' ').Last();
        if (kind is "Income" or "Expense" or "Transfer") { var symbol = Ui.Icon(kind == "Income" ? "arrow_up" : kind == "Expense" ? "arrow_down" : "arrow_swap", 36, kind == "Income" ? Ui.Green : kind == "Expense" ? Ui.Red : Ui.Ink); symbol.Margin = new Thickness(0,0,16,0); heading.Children.Add(symbol); }
        heading.Children.Add(Ui.Text(title, 24, bold: true)); header.Children.Add(heading);
        var sub = Ui.Text(subtitle, 13, Ui.Muted); sub.Margin = new Thickness(0,8,0,20); header.Children.Add(sub);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = Ui.Stack(); Error.Margin = new Thickness(0,12,0,8); footer.Children.Add(Error);
        var row = Ui.Stack(Orientation.Horizontal); row.HorizontalAlignment = HorizontalAlignment.Right;
        var cancel = Ui.Button("Cancel", () => DialogResult = false); cancel.IsCancel = true;
        Submit = Ui.Button(buttonText, () => { }, true); Submit.IsDefault = true;
        row.Children.Add(cancel); row.Children.Add(Submit); footer.Children.Add(row);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = Fields, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Fields.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_, _) => Error.Text = ""));
        Fields.AddHandler(Selector.SelectionChangedEvent,new SelectionChangedEventHandler((_, _) => Error.Text = ""));
    }
    protected void Field(string label, UIElement input)
    {
        var row = new Grid { Margin = new Thickness(0,6,0,6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(124) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        var text = Ui.Text(label, 13, Ui.Ink); text.Margin = new Thickness(0,0,12,0); row.Children.Add(text);
        Grid.SetColumn(input,1); row.Children.Add(input); Fields.Children.Add(row);
    }
    protected void Attempt(Action action)
    {
        try { action(); } catch (Exception e) when (e is InvalidDataException or FormatException or OverflowException) { Error.Text = e.Message; }
    }
    public void AddFields(UIElement element) => Fields.Children.Add(element);
    public void OnSubmit(Action action) => Submit.Click += (_, _) => Attempt(() => { action(); DialogResult = true; });
    public static TextBox MoneyInput(decimal value = 0) => new MoneyTextBox(value);
    public static TextBox DateInput(DateOnly value) => new() { Text = Display.Date(value), ToolTip = "MM/DD/YYYY — month/day/year", MaxLength = 10 };
    protected void DateField(string label, TextBox input)
        => Field(label,DatePickerInput(input));
    public static UIElement DatePickerInput(TextBox input)
    {
        var row = new DockPanel(); var button = Ui.SquareIconButton("calendar", () => { }, "Choose date"); button.Margin = new Thickness(4,0,0,0);
        var popup = new Popup { PlacementTarget = button, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true };
        var calendar = new Calendar(); popup.Child = Ui.Box(calendar, new Thickness(4));
        button.Click += (_, _) => { if (DateOnly.TryParseExact(input.Text, "MM/dd/yyyy", Display.Culture, System.Globalization.DateTimeStyles.None, out var parsed)) calendar.SelectedDate = parsed.ToDateTime(TimeOnly.MinValue); popup.IsOpen = true; };
        calendar.SelectedDatesChanged += (_, _) => { if (calendar.SelectedDate is DateTime date) { input.Text = Display.Date(DateOnly.FromDateTime(date)); popup.IsOpen = false; } };
        DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(input); return row;
    }
    protected static DateOnly ReadDate(TextBox input)
    {
        FinanceDocument.Require(DateOnly.TryParseExact(input.Text.Trim(), "MM/dd/yyyy", Display.Culture, System.Globalization.DateTimeStyles.None, out var date), "Enter a valid date in MM/DD/YYYY format."); return date;
    }
    protected static ComboBox Combo(IEnumerable<object> items, string display = "Name") => new() { ItemsSource = items, DisplayMemberPath = display };
}

public sealed class BankDialog : FormWindow
{
    public Bank? Result { get; private set; }
    public BankDialog(Window owner, FinanceDocument doc, Bank? existing = null) : base(owner, existing is null ? "New Bank Account" : "Edit Bank Account", "One account per bank. All balances are in Rupiah.", existing is null ? "Add Bank" : "Apply Changes")
    {
        var name = new TextBox { Text = existing?.Name ?? "", MaxLength = 100 };
        var date = DateInput(existing?.OpeningDate ?? DateOnly.FromDateTime(DateTime.Today));
        var balance = MoneyInput(existing?.OpeningBalance ?? 0);
        Field("Bank Name *", name); DateField("Opening Balance\nDate *", date); Field("Opening Balance\n(Rp) *", balance);
        var note = Ui.Text("The opening balance is the balance before the first transaction on this date.", 12, Ui.Muted); note.Margin = new Thickness(0,12,0,0); Fields.Children.Add(note);
        Submit.Click += (_, _) => Attempt(() => {
            var bank = new Bank(existing?.Id ?? Guid.NewGuid(), name.Text.Trim(), ReadDate(date), MoneyEditing.Read(balance.Text, emptyAsZero: true));
            var copy = doc.Copy(); copy.Banks.RemoveAll(b => b.Id == bank.Id); copy.Banks.Add(bank); copy.Validate();
            if (copy.HasNegativeHistory(bank.Id) && !ConfirmDialog.Confirm(this, "Negative balance", "This opening balance creates a negative balance in this bank's history. Continue?", "Continue")) return;
            Result = bank; DialogResult = true;
        });
        Loaded += (_, _) => name.Focus();
    }
}

public sealed class CategoryDialog : FormWindow
{
    public Category? Result { get; private set; }
    public CategoryDialog(Window owner, FinanceDocument doc, Category? existing = null, CategoryKind? forced = null) : base(owner, existing is null ? "New Category" : "Edit Category", "Create your own income and expense categories.", existing is null ? "Add Category" : "Apply Changes")
    {
        var name = new TextBox { Text = existing?.Name ?? "", MaxLength = 100 };
        var type = new ComboBox { ItemsSource = Enum.GetValues<CategoryKind>(), SelectedItem = forced ?? existing?.Kind ?? CategoryKind.Expense, IsEnabled = forced is null && (existing is null || !doc.Transactions.Any(t => t.CategoryId == existing.Id || t.FeeCategoryId == existing.Id)) };
        Field("Category Name *", name); Field("Type *", type);
        Submit.Click += (_, _) => Attempt(() => {
            var category = new Category(existing?.Id ?? Guid.NewGuid(), name.Text.Trim(), (CategoryKind)type.SelectedItem);
            var copy = doc.Copy(); copy.Categories.RemoveAll(c => c.Id == category.Id); copy.Categories.Add(category); copy.Validate(); Result = category; DialogResult = true;
        });
        Loaded += (_, _) => name.Focus();
    }
}

public sealed class TransactionDialog : FormWindow
{
    public TransactionDialog(Window owner, DocumentSession session, TransactionKind kind, Transaction? existing = null,Guid? debtPreset=null,bool trackNewDebt=false) : base(owner, (existing is null ? "New " : "Edit ") + kind, kind == TransactionKind.Transfer ? "Move money between your own bank accounts. Fields marked * are required." : "Fields marked * are required.", existing is null ? "Add Transaction" : "Apply Changes", kind == TransactionKind.Transfer ? Math.Min(1120,SystemParameters.WorkArea.Width - 48) : 560)
    {
        var doc = session.Document;
        AllocationEditor? allocations = null;
        if (kind == TransactionKind.Transfer) MinWidth = 900;
        var date = DateInput(existing?.Date ?? DateOnly.FromDateTime(DateTime.Today));
        var banks = doc.Banks.OrderBy(b => b.Name).ToList();
        var presetDebt=doc.Debts.FirstOrDefault(d=>d.Id==debtPreset);
        var bank = Combo(banks); bank.SelectedItem = banks.FirstOrDefault(b => b.Id == existing?.BankId) ?? banks.FirstOrDefault();
        var target = Combo(banks); target.SelectedItem = banks.FirstOrDefault(b => b.Id == existing?.ToBankId) ?? banks.FirstOrDefault(b => b.Id != (bank.SelectedItem as Bank)?.Id);
        if(existing is null&&kind==TransactionKind.Transfer&&presetDebt is {Kind:DebtKind.Self}){
            if(trackNewDebt){bank.SelectedItem=banks.First(b=>b.Id==presetDebt.SelfBankId);target.SelectedItem=banks.FirstOrDefault(b=>b.Id!=presetDebt.SelfBankId);}
            else{target.SelectedItem=banks.First(b=>b.Id==presetDebt.SelfBankId);bank.SelectedItem=banks.FirstOrDefault(b=>b.Id!=presetDebt.SelfBankId);}
        }
        else if(existing is null&&kind!=TransactionKind.Transfer&&presetDebt is {Kind:DebtKind.Self})bank.SelectedItem=banks.First(b=>b.Id==presetDebt.SelfBankId);
        var amount = MoneyInput(existing?.Amount ?? 0);
        var fee = MoneyInput(existing?.Fee ?? 0);
        var categories = new ComboBox { DisplayMemberPath = "Name" };
        void RefreshCategories(ComboBox combo, CategoryKind categoryKind, Guid? selected)
        {
            var items = session.Document.Categories.Where(c => c.Kind == categoryKind).OrderBy(c => c.Name).ToList();
            combo.ItemsSource = items; combo.SelectedItem = items.FirstOrDefault(c => c.Id == selected);
        }
        var catKind = kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense;
        RefreshCategories(categories, catKind, existing?.CategoryId);
        DockPanel CategoryInput(ComboBox combo, CategoryKind categoryKind)
        {
            var panel = new DockPanel(); var add = Ui.Button("New", () => {
                var dialog = new CategoryDialog(this, session.Document, forced: categoryKind);
                if (dialog.ShowDialog() == true) { var copy = session.Document.Copy(); copy.Categories.Add(dialog.Result!); session.Apply(copy); RefreshCategories(combo, categoryKind, dialog.Result!.Id); }
            }); add.Foreground = Ui.Accent; add.Padding = new Thickness(8,0,8,0); add.Margin = new Thickness(4,0,0,0); add.BorderThickness = new Thickness(0); add.MinWidth = 52; add.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(add, Dock.Right); panel.Children.Add(add); panel.Children.Add(combo); return panel;
        }
        DateField("Date *", date); Field(kind == TransactionKind.Transfer ? "From Bank *" : "Bank *", bank);
        if (kind == TransactionKind.Transfer) Field("To Bank *", target); else Field("Category *", CategoryInput(categories, catKind));
        var incomeBasis = new ComboBox { ItemsSource = new[] { "Before fee (gross)", "After fee (net received)" }, SelectedIndex = (int)(existing?.IncomeBasis ?? IncomeAmountBasis.BeforeFee) };
        if (kind == TransactionKind.Income) Field("Amount basis *", incomeBasis);
        Field("Amount (Rp) *", amount);
        var summary = Ui.Text("", 13, Ui.Muted);
        Field(kind == TransactionKind.Transfer ? "Transfer Fee (Rp)" : "Admin Fee (Rp)", fee);
        void UpdateSummary()
        {
            try
            {
                var a = MoneyEditing.Read(amount.Text, emptyAsZero: true); var f = MoneyEditing.Read(fee.Text, emptyAsZero: true);
                if (kind == TransactionKind.Transfer) summary.Text = $"From bank: −{Display.Money(a + f)}\nTo bank: +{Display.Money(a)}";
                else if (kind == TransactionKind.Expense) summary.Text = $"Expense: {Display.Money(a)}\nAdmin fee: {Display.Money(f)}\nTotal deducted: {Display.Money(a + f)}";
                else
                {
                    var gross = incomeBasis.SelectedIndex == 1 ? a + f : a;
                    summary.Text = $"Gross income: {Display.Money(gross)}\nAdmin fee: {Display.Money(f)}\nNet received: {Display.Money(gross - f)}";
                }
            }
            catch (InvalidDataException) { summary.Text = "Enter a valid amount and fee to see the summary."; }
        }
        amount.TextChanged += (_, _) => UpdateSummary(); fee.TextChanged += (_, _) => UpdateSummary(); incomeBasis.SelectionChanged += (_, _) => UpdateSummary(); UpdateSummary();
        var description = new TextBox { Text = existing?.Description ?? "", AcceptsReturn = true, Height = 64, TextWrapping = TextWrapping.Wrap, MaxLength = 2000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Field("Description", description);
        DebtTrackingEditor? debtEditor=null;
        if(kind!=TransactionKind.Transfer){
            debtEditor=new DebtTrackingEditor(session,kind,existing,()=>ReadDate(date),()=>(bank.SelectedItem as Bank)?.Id,()=>MoneyEditing.Read(amount.Text,true),()=>MoneyEditing.Read(fee.Text,true),()=>incomeBasis.SelectedIndex==1,()=>Error.Text="",debtPreset,trackNewDebt);
            Fields.Children.Add(Ui.Box(debtEditor,new Thickness(12)));
            date.TextChanged+=(_,_)=>debtEditor.Refresh();amount.TextChanged+=(_,_)=>debtEditor.Refresh();fee.TextChanged+=(_,_)=>debtEditor.Refresh();incomeBasis.SelectionChanged+=(_,_)=>debtEditor.Refresh();
            bank.SelectionChanged+=(_,_)=>debtEditor.Refresh();
        }
        if (kind == TransactionKind.Transfer)
        {
            var originalFields = Fields.Children.Cast<UIElement>().ToArray(); Fields.Children.Clear();
            var details = Ui.Stack(); var heading = Ui.Text("Transfer Details",18,bold:true); heading.Margin = new Thickness(0,0,0,16); details.Children.Add(heading);
            foreach (var field in originalFields) details.Children.Add(field);
            var columns = new Grid(); columns.ColumnDefinitions.Add(new ColumnDefinition()); columns.ColumnDefinitions.Add(new ColumnDefinition());
            details.Margin = new Thickness(0,0,24,0); columns.Children.Add(details);
            allocations = new AllocationEditor(this,session,() => MoneyEditing.Read(amount.Text,emptyAsZero:true),()=>(bank.SelectedItem as Bank)?.Id,existing?.Allocations,existing is null?debtPreset:null,existing is null&&trackNewDebt);
            var right = new Border { BorderBrush = Ui.Line, BorderThickness = new Thickness(1,0,0,0), Padding = new Thickness(24,0,0,0), Child = allocations }; Grid.SetColumn(right,1); columns.Children.Add(right); Fields.Children.Add(columns);
            amount.TextChanged += (_, _) => allocations.UpdateTotals();
            bank.SelectionChanged+=(_,_)=>allocations.UpdateTotals();
        }
        summary.Margin = new Thickness(0,8,0,0); Fields.Children.Add(Ui.Box(summary, new Thickness(12), Ui.Brush("#F6F6F9"))); var feeHint = Ui.Text("Fees are recorded automatically as a linked Admin Fee expense.",12,Ui.Muted); feeHint.Margin = new Thickness(0,12,0,0); Fields.Children.Add(feeHint);
        Submit.Click += (_, _) => Attempt(() => {
            var sourceBank = bank.SelectedItem as Bank;
            FinanceDocument.Require(sourceBank is not null, "Choose a bank.");
            var parsedFee = MoneyEditing.Read(fee.Text, emptyAsZero: true);
            var tracking=debtEditor?.Read()??new DebtTrackingResult();
            var allocated=allocations?.Read();
            var item = new Transaction(existing?.Id ?? Guid.NewGuid(), ReadDate(date), kind, sourceBank.Id,
                kind == TransactionKind.Transfer ? (target.SelectedItem as Bank)?.Id : null,
                kind == TransactionKind.Transfer ? null : (categories.SelectedItem as Category)?.Id,
                MoneyEditing.Read(amount.Text), parsedFee, null, description.Text.Trim(), allocated?.Allocations, kind == TransactionKind.Income ? (IncomeAmountBasis)incomeBasis.SelectedIndex : IncomeAmountBasis.BeforeFee,tracking.DebtId,tracking.SelfPayment);
            var copy = DebtService.SaveTransaction(session.Document,item,tracking.NewDebt,allocated?.NewDebts);
            var affected = new HashSet<Guid> { item.BankId }; if (item.ToBankId is Guid dest) affected.Add(dest);
            if (existing is not null) { affected.Add(existing.BankId); if (existing.ToBankId is Guid oldTarget) affected.Add(oldTarget); }
            var negative = affected.Where(copy.HasNegativeHistory).ToList();
            if (negative.Count > 0 && !ConfirmDialog.Confirm(this, "Negative balance", "This transaction results in a negative balance in the recorded history of: " + string.Join(", ", negative.Select(copy.BankName)) + ".\n\nBalance after this transaction date:\n" + string.Join("\n", affected.Select(id => copy.BankName(id) + ": " + Display.Money(copy.Balance(id, item.Date)))) + "\n\nAdd it anyway?", "Continue")) return;
            session.Apply(copy); DialogResult = true;
        });
    }
}

public sealed class ManagerWindow : Window
{
    public ManagerWindow(Window owner, DocumentSession session, bool banks, Action changed)
    {
        Ui.ApplyWindowStyle(this);
        Owner = owner; Title = (banks ? "Bank Accounts" : "Categories") + " — Piggy"; Width = 780; Height = 520; MinWidth = 560; MinHeight = 360; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var heading = Ui.Text(banks ? "Bank Accounts" : "Categories", 22, bold: true); heading.Margin = new Thickness(0,0,0,16); DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        var grid = new DataGrid();
        void Refresh() { grid.ItemsSource = banks ? session.Document.Banks.OrderBy(b => b.Name).Select(b => new ManagerRow(b.Id, b.Name, Display.Date(b.OpeningDate), Display.Money(b.OpeningBalance))).ToList() : session.Document.Categories.OrderBy(c => c.Kind).ThenBy(c => c.Name).Select(c => new ManagerRow(c.Id, c.Name, c.Kind.ToString(), "")).ToList(); changed(); }
        void Edit(bool create)
        {
            var selected = grid.SelectedItem as ManagerRow; if (!create && selected is null) return;
            var copy = session.Document.Copy();
            if (banks) { var dialog = new BankDialog(this, session.Document, create ? null : session.Document.Banks.First(b => b.Id == selected!.Id)); if (dialog.ShowDialog() != true) return; copy.Banks.RemoveAll(b => b.Id == dialog.Result!.Id); copy.Banks.Add(dialog.Result!); }
            else { var dialog = new CategoryDialog(this, session.Document, create ? null : session.Document.Categories.First(c => c.Id == selected!.Id)); if (dialog.ShowDialog() != true) return; copy.Categories.RemoveAll(c => c.Id == dialog.Result!.Id); copy.Categories.Add(dialog.Result!); }
            session.Apply(copy); Refresh();
        }
        var controls = Ui.Stack(Orientation.Horizontal); controls.Children.Add(Ui.Button(banks ? "Add Bank" : "Add Category", () => Edit(true), true));
        var edit = Ui.Button("Edit", () => Edit(false)); edit.IsEnabled = false; controls.Children.Add(edit);
        var delete = Ui.Button("Delete", () => {
            if (grid.SelectedItem is not ManagerRow selected) return;
            var used = session.Document.Transactions.Any(t => banks ? t.BankId == selected.Id || t.ToBankId == selected.Id : t.CategoryId == selected.Id || t.FeeCategoryId == selected.Id);
            used |= banks && session.Document.WishlistEvents.Any(e=>e.FromBankId==selected.Id||e.ToBankId==selected.Id);
            if (used) { ConfirmDialog.Alert(this, "Cannot delete", "This item is used by recorded transactions or wishlist history and must be retained."); return; }
            if (!ConfirmDialog.Confirm(this, "Delete " + (banks ? "bank" : "category"), $"Delete {selected.Name}?", "Delete")) return;
            var copy = session.Document.Copy(); if (banks) copy.Banks.RemoveAll(b => b.Id == selected.Id); else copy.Categories.RemoveAll(c => c.Id == selected.Id); session.Apply(copy); Refresh();
        }); delete.IsEnabled = false; controls.Children.Add(delete);
        grid.SelectionChanged += (_, _) => { edit.IsEnabled = delete.IsEnabled = grid.SelectedItem is not null; }; grid.MouseDoubleClick += (_, e) => { if (e.OriginalSource is DependencyObject d && FindRow(d) is not null) Edit(false); };
        DockPanel.SetDock(controls, Dock.Top); root.Children.Add(controls);
        var close = Ui.Button("Done", Close); close.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
        grid.Columns.Add(Ui.Column(banks ? "Bank Name" : "Category Name", "Name", 2)); grid.Columns.Add(Ui.Column(banks ? "Opening Date" : "Type", "Detail", 1)); if (banks) grid.Columns.Add(Ui.Column("Opening Balance", "Value", 1.5, true)); root.Children.Add(grid); Refresh();
    }
    private static DataGridRow? FindRow(DependencyObject? element) { while (element is not null && element is not DataGridRow) element = VisualTreeHelper.GetParent(element); return element as DataGridRow; }
    private sealed record ManagerRow(Guid Id, string Name, string Detail, string Value);
}

public sealed class ConfirmDialog : Window
{
    private ConfirmDialog(Window owner, string heading, string message)
    {
        Ui.ApplyWindowStyle(this);
        Owner = owner; Title = "Piggy"; Width = 540; SizeToContent = SizeToContent.Height; MaxHeight = SystemParameters.WorkArea.Height - 40; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var stack = Ui.Stack(); stack.Margin = new Thickness(20); Content = stack;
        stack.Children.Add(Ui.Text(heading, 20, bold: true)); var text = Ui.Text(message, 14, Ui.Muted); text.Margin = new Thickness(0,16,0,24); stack.Children.Add(text);
        Buttons.HorizontalAlignment = HorizontalAlignment.Right; stack.Children.Add(Buttons);
    }
    private readonly StackPanel Buttons = Ui.Stack(Orientation.Horizontal);
    public static bool Confirm(Window owner, string heading, string message, string action)
    {
        var dialog = new ConfirmDialog(owner, heading, message); var cancel = Ui.Button("Cancel", () => dialog.DialogResult = false); cancel.IsCancel = true;
        dialog.Buttons.Children.Add(cancel); dialog.Buttons.Children.Add(Ui.Button(action, () => dialog.DialogResult = true, true)); return dialog.ShowDialog() == true;
    }
    public static void Alert(Window owner, string heading, string message)
    {
        var dialog = new ConfirmDialog(owner, heading, message); var ok = Ui.Button("OK", () => dialog.DialogResult = true, true); ok.IsDefault = true; dialog.Buttons.Children.Add(ok); dialog.ShowDialog();
    }
    public static string Unsaved(Window owner, string name)
    {
        var result = "Cancel"; var dialog = new ConfirmDialog(owner, "Save changes?", $"Do you want to save changes to {name}?\n\nYour changes will be lost if you don't save them.");
        foreach (var label in new[] { "Save", "Don't Save", "Cancel" }) { var choice = label; var button = Ui.Button(label, () => { result = choice; dialog.DialogResult = true; }, label == "Save"); if (label == "Cancel") button.IsCancel = true; dialog.Buttons.Children.Add(button); }
        dialog.ShowDialog(); return result;
    }
}

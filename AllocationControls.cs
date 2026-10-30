using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed class PurposeDialog : FormWindow
{
    public Purpose? Result { get; private set; }
    public PurposeDialog(Window owner, FinanceDocument doc, Purpose? existing = null, string initialName = "")
        : base(owner, existing is null ? "New Purpose" : "Edit Purpose", "A purpose describes how a transfer will be used.", existing is null ? "Create Purpose" : "Apply Changes")
    {
        var name = new TextBox { Text = existing?.Name ?? initialName, MaxLength = 100 };
        Field("Purpose Name *", name);
        Submit.Click += (_, _) => Attempt(() => {
            var purpose = new Purpose(existing?.Id ?? Guid.NewGuid(), name.Text.Trim());
            var copy = doc.Copy(); copy.Purposes.RemoveAll(p => p.Id == purpose.Id); copy.Purposes.Add(purpose); copy.Validate();
            Result = purpose; DialogResult = true;
        });
        Loaded += (_, _) => name.Focus();
    }
}

public sealed class PurposeManagerWindow : Window
{
    public PurposeManagerWindow(Window owner, DocumentSession session, Action changed)
    {
        Ui.ApplyWindowStyle(this); Owner = owner; Title = "Purposes — Piggy";
        Width = 680; Height = 500; MinWidth = 500; MinHeight = 340; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(24) }; Content = root;
        var heading = Ui.Text("Purposes",22,bold:true); heading.Margin = new Thickness(0,0,0,12); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
        var help = Ui.Text("Labels for transfer allocations, separate from income and expense categories.",14,Ui.Muted); help.Margin = new Thickness(0,0,0,16); DockPanel.SetDock(help,Dock.Top); root.Children.Add(help);
        var list = new DataGrid(); list.Columns.Add(Ui.Column("Purpose Name","Name",1));
        void Refresh() { list.ItemsSource = session.Document.Purposes.OrderBy(p => p.Name).ToList(); changed(); }
        void Edit(bool create)
        {
            if (!create && list.SelectedItem is not Purpose) return;
            var dialog = new PurposeDialog(this,session.Document,create ? null : (Purpose)list.SelectedItem);
            if (dialog.ShowDialog() != true) return;
            var copy = session.Document.Copy(); copy.Purposes.RemoveAll(p => p.Id == dialog.Result!.Id); copy.Purposes.Add(dialog.Result!); session.Apply(copy); Refresh();
        }
        var actions = Ui.Stack(Orientation.Horizontal); actions.Children.Add(Ui.Button("New Purpose",() => Edit(true),true));
        var edit = Ui.Button("Edit",() => Edit(false)); edit.IsEnabled = false; actions.Children.Add(edit);
        var delete = Ui.Button("Delete",() => {
            if (list.SelectedItem is not Purpose purpose) return;
            if (session.Document.Transactions.Any(t => t.Allocations?.Any(a => a.PurposeId == purpose.Id) == true))
            { ConfirmDialog.Alert(this,"Cannot delete","This purpose is used by transfer allocations. Edit or remove those allocations first."); return; }
            if (!ConfirmDialog.Confirm(this,"Delete purpose?",$"Delete {purpose.Name}?","Delete")) return;
            var copy = session.Document.Copy(); copy.Purposes.RemoveAll(p => p.Id == purpose.Id); session.Apply(copy); Refresh();
        }); delete.IsEnabled = false; actions.Children.Add(delete);
        list.SelectionChanged += (_, _) => edit.IsEnabled = delete.IsEnabled = list.SelectedItem is Purpose;
        actions.Margin = new Thickness(0,0,0,12); DockPanel.SetDock(actions,Dock.Top); root.Children.Add(actions);
        var done = Ui.Button("Done",Close); done.HorizontalAlignment = HorizontalAlignment.Right; DockPanel.SetDock(done,Dock.Bottom); root.Children.Add(done); root.Children.Add(list); Refresh();
    }
}

public sealed class AllocationEditor : StackPanel
{
    private sealed record DebtChoice(Guid? Id,string Name);
    private sealed record AllocationRow(ComboBox Purpose,MoneyTextBox Amount,Button Create,ComboBox Action,ComboBox Debt,StackPanel DebtPanel,StackPanel NewFields,TextBox Name,TextBox Due,TextBlock Origin,Guid? ExistingDebtId);
    private readonly List<AllocationRow> rows=[];
    private readonly StackPanel rowPanel=Ui.Stack();
    private readonly TextBlock allocated=Ui.Text("",16,Ui.Ink,true),remaining=Ui.Text("",16);
    private readonly Window owner;
    private readonly DocumentSession session;
    private readonly Func<decimal> transferAmount;
    private readonly Func<Guid?> sourceBank;
    private bool refreshing;

    public AllocationEditor(Window owner,DocumentSession session,Func<decimal> transferAmount,Func<Guid?> sourceBank,IReadOnlyList<TransferAllocation>? existing,Guid? selfDebtPreset=null,bool borrowNew=false)
    {
        this.owner=owner;this.session=session;this.transferAmount=transferAmount;this.sourceBank=sourceBank;
        Children.Add(Ui.Text("Allocations",18,bold:true));
        var help=Ui.Text("Optionally split this transfer by purpose. Admin fee is excluded.",14,Ui.Muted);help.Margin=new Thickness(0,12,0,20);Children.Add(help);
        var headers=new Grid();headers.ColumnDefinitions.Add(new ColumnDefinition());headers.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(156)});headers.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});
        headers.Children.Add(Ui.Text("Purpose",14,Ui.Muted));var amountHeader=Ui.Text("Amount (Rp)",14,Ui.Muted);Grid.SetColumn(amountHeader,1);headers.Children.Add(amountHeader);headers.Margin=new Thickness(0,0,0,8);Children.Add(headers);Children.Add(rowPanel);
        var add=Ui.Button("+ Add allocation",()=>AddRow());add.Foreground=Ui.Accent;add.HorizontalAlignment=HorizontalAlignment.Left;add.BorderThickness=new Thickness(0);add.Margin=new Thickness(0,12,0,0);add.Padding=new Thickness(0,8,12,8);Children.Add(add);
        var create=Ui.Button("New purpose",()=>{var dialog=new PurposeDialog(owner,session.Document);if(dialog.ShowDialog()!=true)return;StorePurpose(dialog.Result!);AddRow(dialog.Result!.Id);});create.Foreground=Ui.Accent;create.HorizontalAlignment=HorizontalAlignment.Left;create.BorderThickness=new Thickness(0);create.Margin=new Thickness(0);create.Padding=new Thickness(0,8,12,8);Children.Add(create);
        var totals=Ui.Stack();totals.Margin=new Thickness(0,20,0,0);
        void TotalLine(string label,TextBlock value){var line=new DockPanel{Margin=new Thickness(0,10,0,0)};DockPanel.SetDock(value,Dock.Right);line.Children.Add(value);line.Children.Add(Ui.Text(label,14));totals.Children.Add(line);}
        TotalLine("Allocated total",allocated);TotalLine("Remaining",remaining);Children.Add(new Border{BorderBrush=Ui.Line,BorderThickness=new Thickness(0,1,0,0),Child=totals});
        var note=Ui.Text("Purpose only is a note. Borrow as Self Debt takes funds from From Bank; Repay Self Debt returns funds to To Bank. Select the debt explicitly. Admin fees never change principal.",12,Ui.Muted);note.Margin=new Thickness(0,20,0,0);Children.Add(note);
        if(existing is not null)foreach(var allocation in existing)AddRow(allocation.PurposeId,allocation.Amount,allocation.SelfDebtId,allocation.DebtAction);
        if(selfDebtPreset is not null||borrowNew)AddRow(selfDebtId:selfDebtPreset,debtAction:borrowNew?SelfDebtAction.Borrow:SelfDebtAction.Repay,borrowNew:borrowNew);
        UpdateTotals();
    }
    private void StorePurpose(Purpose purpose){var copy=session.Document.Copy();copy.Purposes.Add(purpose);session.Apply(copy);RefreshChoices();}
    private void RefreshChoices()
    {
        refreshing=true;
        try{foreach(var row in rows){var name=row.Purpose.Text;row.Purpose.ItemsSource=session.Document.Purposes.OrderBy(p=>p.Name).ToList();row.Purpose.SelectedItem=session.Document.Purposes.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));row.Purpose.Text=row.Purpose.SelectedItem is Purpose p?p.Name:name;}}
        finally{refreshing=false;}foreach(var row in rows)UpdateCreate(row);
    }
    private void UpdateCreate(AllocationRow row)
    {
        if(refreshing)return;var name=row.Purpose.Text.Trim();row.Create.Visibility=name.Length>0&&!session.Document.Purposes.Any(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase))?Visibility.Visible:Visibility.Collapsed;
    }
    private void LoadDebts(AllocationRow row,Guid? selected=null)
    {
        var options=session.Document.Debts.Where(d=>d.Kind==DebtKind.Self&&(!d.IsPaid||d.Id==row.ExistingDebtId)).OrderBy(d=>d.Name).Select(d=>new DebtChoice(d.Id,session.Document.DebtLabel(d.Id))).ToList();
        if(row.Action.SelectedIndex==1)options.Insert(0,new(null,"New Self Debt"));row.Debt.ItemsSource=options;
        row.Debt.SelectedItem=selected is Guid id?options.FirstOrDefault(d=>d.Id==id):row.Action.SelectedIndex==1?options.First():null;UpdateDebtFields(row);
    }
    private void UpdateDebtFields(AllocationRow row)
    {
        row.DebtPanel.Visibility=row.Action.SelectedIndex==0?Visibility.Collapsed:Visibility.Visible;
        row.NewFields.Visibility=row.Action.SelectedIndex==1&&row.Debt.SelectedItem is DebtChoice{Id:null}?Visibility.Visible:Visibility.Collapsed;
        var bank=row.Debt.SelectedItem is DebtChoice{Id:Guid id}?session.Document.Debts.First(d=>d.Id==id).SelfBankId:sourceBank();
        row.Origin.Text=bank is Guid value?"Return to: "+session.Document.BankName(value):"Choose From Bank for the original account.";
    }
    public void AddRow(Guid? purposeId=null,decimal amount=0,Guid? selfDebtId=null,SelfDebtAction debtAction=SelfDebtAction.Repay,bool borrowNew=false)
    {
        var purpose=new ComboBox{Style=(Style)Application.Current.FindResource("PurposeComboStyle"),IsEditable=true,IsTextSearchEnabled=false,DisplayMemberPath="Name",ItemsSource=session.Document.Purposes.OrderBy(p=>p.Name).ToList(),ToolTip="Choose a purpose or type a new name, then click Create purpose."};
        if(purposeId is Guid id)purpose.SelectedItem=session.Document.Purposes.FirstOrDefault(p=>p.Id==id);
        var input=new MoneyTextBox(amount){Margin=new Thickness(8,0,0,0)};
        var create=Ui.Button("Create purpose",()=>{});create.Foreground=Ui.Accent;create.HorizontalAlignment=HorizontalAlignment.Left;create.Margin=new Thickness(0);create.Padding=new Thickness(0,6,8,6);create.BorderThickness=new Thickness(0);create.Visibility=Visibility.Collapsed;
        var action=new ComboBox{ItemsSource=new[]{"Purpose only","Borrow as Self Debt","Repay Self Debt"},SelectedIndex=selfDebtId is null&&!borrowNew?0:debtAction==SelfDebtAction.Borrow?1:2,Margin=new Thickness(0,4,0,4)};
        var debt=new ComboBox{DisplayMemberPath="Name"};var debtPanel=Ui.Stack();debtPanel.Children.Add(debt);
        var newFields=Ui.Stack();var name=new TextBox{MaxLength=100};var due=FormWindow.DateInput(DateOnly.FromDateTime(DateTime.Today));due.Text="";
        newFields.Children.Add(Ui.Text("Debt Name *",12,Ui.Muted));newFields.Children.Add(name);var dueLabel=Ui.Text("Due Date",12,Ui.Muted);dueLabel.Margin=new Thickness(0,8,0,0);newFields.Children.Add(dueLabel);newFields.Children.Add(FormWindow.DatePickerInput(due));debtPanel.Children.Add(newFields);
        var origin=Ui.Text("",12,Ui.Muted);origin.Margin=new Thickness(0,4,0,8);debtPanel.Children.Add(origin);
        var row=new AllocationRow(purpose,input,create,action,debt,debtPanel,newFields,name,due,origin,selfDebtId);rows.Add(row);
        var container=Ui.Stack();var grid=new Grid{Margin=new Thickness(0,4,0,4)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(156)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(40)});grid.Children.Add(purpose);Grid.SetColumn(input,1);grid.Children.Add(input);
        var remove=Ui.IconButton("delete","",()=>{rows.Remove(row);rowPanel.Children.Remove(container);UpdateTotals();},Ui.Muted,false);remove.ToolTip="Remove allocation";remove.Padding=new Thickness(8);remove.Margin=new Thickness(0);Grid.SetColumn(remove,2);grid.Children.Add(remove);
        container.Children.Add(grid);container.Children.Add(create);container.Children.Add(action);container.Children.Add(debtPanel);rowPanel.Children.Add(container);
        create.Click+=(_,_)=>{try{var value=purpose.Text.Trim();var existing=session.Document.Purposes.FirstOrDefault(p=>string.Equals(p.Name,value,StringComparison.OrdinalIgnoreCase));if(existing is null){existing=new Purpose(Guid.NewGuid(),value);StorePurpose(existing);}purpose.SelectedItem=session.Document.Purposes.First(p=>p.Id==existing.Id);purpose.Text=existing.Name;UpdateCreate(row);}catch(InvalidDataException e){ConfirmDialog.Alert(owner,"Could not create purpose",e.Message);}};
        purpose.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,_)=>UpdateCreate(row)));purpose.SelectionChanged+=(_,_)=>UpdateCreate(row);input.TextChanged+=(_,_)=>UpdateTotals();
        action.SelectionChanged+=(_,_)=>LoadDebts(row);debt.SelectionChanged+=(_,_)=>UpdateDebtFields(row);LoadDebts(row,selfDebtId);UpdateTotals();
    }
    public void UpdateTotals()
    {
        foreach(var row in rows)UpdateDebtFields(row);
        try{var total=rows.Sum(row=>MoneyEditing.Read(row.Amount.Text,emptyAsZero:true));var difference=transferAmount()-total;allocated.Text=Display.Money(total);remaining.Text=Display.Money(difference);remaining.Foreground=rows.Count==0?Ui.Muted:difference==0?Ui.Green:Ui.Red;}
        catch(InvalidDataException){allocated.Text="—";remaining.Text="—";}
    }
    public (IReadOnlyList<TransferAllocation> Allocations,IReadOnlyList<Debt> NewDebts) Read()
    {
        var allocations=new List<TransferAllocation>();var newDebts=new List<Debt>();
        foreach(var row in rows){
            var name=row.Purpose.Text.Trim();var purpose=session.Document.Purposes.FirstOrDefault(p=>string.Equals(p.Name,name,StringComparison.OrdinalIgnoreCase));FinanceDocument.Require(purpose is not null,"Choose a purpose, or click Create purpose for a new name.");
            Guid? debtId=null;var action=SelfDebtAction.Repay;
            if(row.Action.SelectedIndex!=0){
                FinanceDocument.Require(row.Debt.SelectedItem is DebtChoice,"Choose a Self Debt for each marked allocation.");var selected=(DebtChoice)row.Debt.SelectedItem;
                action=row.Action.SelectedIndex==1?SelfDebtAction.Borrow:SelfDebtAction.Repay;debtId=selected.Id;
                if(debtId is null){
                    FinanceDocument.Require(action==SelfDebtAction.Borrow,"Choose an existing Self Debt for repayment.");DateOnly? due=null;
                    if(!string.IsNullOrWhiteSpace(row.Due.Text)){FinanceDocument.Require(DateOnly.TryParseExact(row.Due.Text.Trim(),"MM/dd/yyyy",Display.Culture,System.Globalization.DateTimeStyles.None,out var parsed),"Enter a valid due date in MM/DD/YYYY format.");due=parsed;}
                    var created=new Debt(Guid.NewGuid(),row.Name.Text.Trim(),"",due,Kind:DebtKind.Self,SelfBankId:sourceBank());newDebts.Add(created);debtId=created.Id;
                }
            }
            allocations.Add(new(purpose.Id,MoneyEditing.Read(row.Amount.Text),debtId,action));
        }
        return(allocations,newDebts);
    }
}

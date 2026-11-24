using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow
{
    private Guid? selectedDebt,renderedDebt;
    private string? selectedDebtGroup;
    private ScrollViewer? debtDetailScroll,debtListScroll;
    private readonly Dictionary<Guid,double> debtOffsets=[];
    private double debtListOffset;
    private Debt? ActiveDebt => selectedDebtGroup is null ? session.Document.Debts.FirstOrDefault(d=>d.Id==selectedDebt) : null;
    private static bool SameDebtGroup(string? first, string? second) => StringComparer.OrdinalIgnoreCase.Equals(first, second);
    private List<Debt> VisibleDebts() => session.Document.Debts.Where(d=>selectedBank is null||session.Document.Transactions.Any(t=>session.Document.RelatedDebtIds(t).Contains(d.Id)&&(t.BankId==selectedBank||t.ToBankId==selectedBank))).ToList();
    private IEnumerable<string> VisibleDebtGroups(List<Debt> debts) => selectedBank is null
        ? session.Document.GroupNames()
        : debts.Select(d => d.GroupName).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase);

    private void NewDebtGroup()
    {
        var dialog = new FormWindow(DetailOwner, "New Debt Group", "Enter a name for the new debt group.", "Create Group");
        var input = new TextBox { MaxLength = 100 };
        dialog.AddFields(input);
        dialog.OnSubmit(() => {
            var name = input.Text.Trim();
            FormValidation.Require(name.Length > 0, input, "Enter a group name.");
            var doc = session.Document.Copy();
            FormValidation.Require(!doc.GroupNames().Contains(name, StringComparer.OrdinalIgnoreCase), input, "This debt group already exists.");
            doc.DebtGroups.Add(name);
            session.Apply(doc);
            selectedBank = null;
            selectedDebtGroup = name;
        });
        if (dialog.ShowDialog() == true) Render();
    }

    private void ShowDebt(){page="Debt";Render();}
    private void CaptureDebtViewport()
    {
        if(debtDetailScroll?.IsLoaded==true&&renderedDebt is Guid id)debtOffsets[id]=debtDetailScroll.VerticalOffset;
        if(debtListScroll?.IsLoaded==true)debtListOffset=debtListScroll.VerticalOffset;debtDetailScroll=null;debtListScroll=null;
    }
    private void ResetDebtView(){selectedDebt=null;selectedDebtGroup=null;renderedDebt=null;debtDetailScroll=null;debtListScroll=null;debtOffsets.Clear();debtListOffset=0;}
    private UIElement DebtPage()
    {
        var doc=session.Document;var debts=VisibleDebts();var root=new DockPanel{Background=Theme.Background};
        var heading=Theme.SectionHeader("Debt","New Debt",()=>DebtAction("new"),"As of "+Display.Date(DateOnly.FromDateTime(DateTime.Today)), Theme.ActionButton("New Debt Group", NewDebtGroup));DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var totalsCard=DebtSummary(debts.Select(d => doc.DebtAmounts(d.Id)));totalsCard.Margin=new Thickness(12,8,12,0);DockPanel.SetDock(totalsCard,Dock.Top);root.Children.Add(totalsCard);
        var hint=Theme.Label(selectedBank is null?"Income and Expenses include the original transactions. Admin fees do not change debt principal.":"Account filter shows related debts. Each debt total includes all its linked accounts.",12,Theme.Muted);hint.Margin=new Thickness(12,8,12,8);DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        if(debts.Count==0 && !VisibleDebtGroups(debts).Any()){var empty=Ui.Stack();empty.Children.Add(Theme.Label(doc.Debts.Count==0?"Track a debt":"No debts related to this account",20,bold:true));empty.Children.Add(Theme.Label("Use New Debt, or edit an existing Expense to track Self Debt. Income can track borrowing from a lender. Tracking does not create additional bank movements.",14,Theme.Muted));var card=Theme.Card(empty,new Thickness(20));card.VerticalAlignment=VerticalAlignment.Top;card.Margin=new Thickness(12);root.Children.Add(card);return root;}
        var id = ActiveDebt?.Id;
        var changed = renderedDebt != id;
        renderedDebt = id;
        var split=new Grid{Margin=new Thickness(12)};split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(248),MinWidth=184});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=380});
        Ui.ConstrainSplitList(root,split);
        var list=Ui.Stack();FrameworkElement? selectedButton=null;
        var unpaidGroups = doc.Debts.Where(d => !d.IsPaid).Select(d => d.GroupName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var populatedGroups = doc.Debts.Select(d => d.GroupName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var groupName in VisibleDebtGroups(debts).OrderBy(name => name))
        {
            var button = Theme.ActionButton(groupName, () => { selectedDebtGroup = groupName; Render(); });
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            Theme.StyleButton(button, selected: SameDebtGroup(selectedDebtGroup, groupName));
            if (populatedGroups.Contains(groupName) && !unpaidGroups.Contains(groupName))
            {
                var row = new DockPanel();
                var check = new TextBlock { Text = "✓", Margin = new Thickness(8, 0, 0, 0), ToolTip = "All debts paid" };
                DockPanel.SetDock(check, Dock.Right);
                row.Children.Add(check);
                row.Children.Add(new TextBlock { Text = groupName, TextTrimming = TextTrimming.CharacterEllipsis });
                button.Content = row;
                button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                System.Windows.Automation.AutomationProperties.SetName(button, groupName + " · All debts paid");
            }
            list.Children.Add(button);
        }
        foreach(var debt in debts.Where(d => d.GroupName is null)){
            var body=Ui.Stack();body.Children.Add(Theme.Label(debt.Name,14,bold:true));body.Children.Add(Theme.Label(debt.Kind==DebtKind.Self?"Self Debt · "+doc.BankName(debt.SelfBankId!.Value):debt.Lender,12,Theme.Muted));body.Children.Add(Theme.Label(DebtService.StatusName(doc.DebtState(debt.Id))+(doc.DebtOverdue(debt.Id)?" · Overdue":""),12,doc.DebtOverdue(debt.Id)?Theme.Error:Theme.Muted));body.Children.Add(Theme.Label(Display.Money(doc.DebtAmounts(debt.Id).Remaining),14,Theme.Accent));
            var button=Ui.Button("",()=>OpenDebt(debt));button.Content=body;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,4);button.Padding=new Thickness(12,10,12,10);Theme.StyleButton(button,selected:debt.Id==id);if(debt.Id==id)selectedButton=button;
            list.Children.Add(button);
        }
        var listScroll=new ScrollViewer{Content=list,Background=Theme.Surface,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};debtListScroll=listScroll;split.Children.Add(Theme.Card(listScroll,new Thickness(8)));var divider=Theme.Splitter();Grid.SetColumn(divider,1);split.Children.Add(divider);
        var content = selectedDebtGroup is string groupKey ? DebtGroupDetail(groupKey, debts.Where(d => SameDebtGroup(d.GroupName, groupKey))) : DebtDetail(ActiveDebt!);
        var detail=new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};debtDetailScroll=detail;
        detail.Loaded+=(_,_)=>Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(!ReferenceEquals(debtDetailScroll,detail)||!detail.IsLoaded)return;listScroll.ScrollToVerticalOffset(debtListOffset);if(changed)selectedButton?.BringIntoView();if(id is Guid debtId)detail.ScrollToVerticalOffset(debtOffsets.GetValueOrDefault(debtId));}));
        var detailCard=Theme.Card(detail,new Thickness(16,12,12,12));Grid.SetColumn(detailCard,2);split.Children.Add(detailCard);root.Children.Add(split);return root;
    }
    private void OpenDebt(Debt debt)
    {
        selectedDebtGroup = null;
        selectedDebt = debt.Id;
        Render();
    }

    private Border DebtSummary(IEnumerable<DebtTotals> totals)
    {
        var amounts = totals.ToArray();
        var grid = new Grid();
        var values = new[] { ("Principal", amounts.Sum(t => t.Borrowed)), ("Principal Paid", amounts.Sum(t => t.Paid)), ("Remaining Debt", amounts.Sum(t => t.Remaining)) };
        for (var i = 0; i < values.Length; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var panel = Ui.Stack();
            panel.Children.Add(Theme.Label(values[i].Item1, 12, Theme.Muted));
            panel.Children.Add(Theme.Label(Display.Money(values[i].Item2), 18, i == 2 ? Theme.Accent : Theme.Text));
            var cell = new Border { Child = panel, Padding = new Thickness(12), BorderBrush = Theme.Rule,
                BorderThickness = new Thickness(0, 0, i < 2 ? 1 : 0, 0) };
            Grid.SetColumn(cell, i);
            grid.Children.Add(cell);
        }
        return Theme.Card(grid, new Thickness(4));
    }

    private UIElement DebtGroupDetail(string name, IEnumerable<Debt> debts)
    {
        var doc = session.Document;
        var members = debts.OrderBy(d => d.Name).Select(d => (Debt: d, Totals: doc.DebtAmounts(d.Id))).ToArray();
        var panel = Ui.Stack();
        panel.Children.Add(Theme.Label(name, 22, bold: true));
        var summary = DebtSummary(members.Select(m => m.Totals));
        summary.Margin = new Thickness(0, 8, 0, 12);
        panel.Children.Add(summary);
        AddRepaymentProgress(panel, members.Sum(m => m.Totals.Paid), members.Sum(m => m.Totals.Borrowed));
        var table = new Grid();
        var widths = new[] { 150, 108, 108, 108, 106 };
        foreach (var width in widths)
            table.ColumnDefinitions.Add(new ColumnDefinition { MinWidth = width, Width = new GridLength(1, GridUnitType.Star) });
        table.ColumnDefinitions.Insert(1, new ColumnDefinition { Width = new GridLength(32) });
        void Row(FrameworkElement debt, bool paid, params string[] values)
        {
            var index = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var rule = new Border { BorderBrush = Theme.Rule, BorderThickness = new Thickness(0, 0, 0, 1) };
            Grid.SetRow(rule, index);
            Grid.SetColumnSpan(rule, table.ColumnDefinitions.Count);
            table.Children.Add(rule);
            Grid.SetRow(debt, index);
            debt.Margin = new Thickness(8, 6, 8, 6);
            table.Children.Add(debt);
            if (paid)
            {
                var check = Theme.Label("✓", 14);
                check.HorizontalAlignment = HorizontalAlignment.Center;
                check.ToolTip = "Debt paid";
                System.Windows.Automation.AutomationProperties.SetName(check, "Debt paid");
                Grid.SetRow(check, index);
                Grid.SetColumn(check, 1);
                table.Children.Add(check);
            }
            for (var i = 0; i < values.Length; i++)
            {
                var label = Theme.Label(values[i], 13, index == 0 ? Theme.Muted : Theme.Text, bold: index == 0);
                label.TextAlignment = i < 3 ? TextAlignment.Right : TextAlignment.Center;
                label.Margin = new Thickness(8, 6, 8, 6);
                Grid.SetRow(label, index);
                Grid.SetColumn(label, i + 2);
                table.Children.Add(label);
            }
        }
        Row(Theme.Label("Debt", 13, Theme.Muted, bold: true), false, "Principal", "Paid", "Remaining", "Due Date");
        foreach (var (debt, totals) in members)
            Row(Theme.ActionButton(debt.Name, () => {
                selectedDebt = debt.Id;
                selectedDebtGroup = null;
                var bank = selectedBank;
                ShowMemberDetail(debt.Name, () => DebtDetail(debt), () => { selectedBank = bank; selectedDebtGroup = name; });
            }), debt.IsPaid, Display.Money(totals.Borrowed), Display.Money(totals.Paid),
                Display.Money(totals.Remaining), debt.DueDate is DateOnly due ? Display.Date(due) : "—");
        panel.Children.Add(new ScrollViewer { Content = table, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        return panel;
    }

    private void AddRepaymentProgress(StackPanel panel, decimal paid, decimal principal)
    {
        if (principal <= 0) return;
        panel.Children.Add(WishlistProgress(paid, principal, "Debt repayment progress", new Thickness(0, 6, 0, 4)));
        panel.Children.Add(Theme.Label($"{paid / principal:P0} repaid", 12, Theme.Muted));
    }

    private UIElement DebtDetail(Debt debt)
    {
        var doc=session.Document;var totals=doc.DebtAmounts(debt.Id);var state=doc.DebtState(debt.Id);var panel=Ui.Stack();
        var heading=new DockPanel();var status=Theme.Label(DebtService.StatusName(state)+(doc.DebtOverdue(debt.Id)?" · Overdue":""),12,doc.DebtOverdue(debt.Id)?Theme.Error:Theme.Muted);DockPanel.SetDock(status,Dock.Right);heading.Children.Add(status);heading.Children.Add(Theme.Label(debt.Name,22,bold:true));panel.Children.Add(heading);
        if (memberPopup is null && debt.GroupName is string groupName)
        {
            var groupLink = Theme.ActionButton("Group · " + groupName, () => { selectedDebtGroup = groupName; Render(); });
            groupLink.HorizontalAlignment = HorizontalAlignment.Left;
            panel.Children.Add(groupLink);
        }
        var self=debt.Kind==DebtKind.Self;
        var info=Theme.Label((self?"Self Debt · Return to: "+doc.BankName(debt.SelfBankId!.Value):"Lender: "+debt.Lender)+"\nDue Date: "+(debt.DueDate is DateOnly due?Display.Date(due):"Not set"),13,Theme.Muted);info.Margin=new Thickness(0,8,0,16);panel.Children.Add(info);
        panel.Children.Add(Theme.Label("Remaining Principal",12,Theme.Muted));panel.Children.Add(Theme.Label(Display.Money(totals.Remaining),24,Theme.Accent));panel.Children.Add(Theme.Label((self?"Funds to Restore: ":"Borrowed: ")+Display.Money(totals.Borrowed)+"    Principal Paid: "+Display.Money(totals.Paid),13));
        var actions=new WrapPanel{Margin=new Thickness(0,16,0,8)};
        AddRepaymentProgress(panel, totals.Paid, totals.Borrowed);
        void Add(string label,string action,bool enabled=true,bool primary=false){var button=Theme.ActionButton(label,()=>DebtAction(action),primary);button.IsEnabled=enabled;button.Margin=new Thickness(0,0,4,4);actions.Children.Add(button);}
        Add(self?"Add Debt":"Add Borrowing","borrow",!debt.IsPaid);Add("Record Payment","payment",!debt.IsPaid&&totals.Remaining>0,true);Add("Edit Details","edit");
        if(debt.IsPaid)Add("Reopen Debt","reopen");else Add("Mark as Paid","paid",totals.Remaining==0);
        var groups = doc.GroupNames().Where(name => !SameDebtGroup(name, debt.GroupName)).OrderBy(name => name).ToArray();
        var move = Theme.ActionButton("Move to Debt Group", () => PickGroup("Move to Debt Group", groups, "", group => {
            session.Apply(DebtService.EditDetails(doc, debt.Id, debt.Name, debt.Lender, debt.DueDate, group));
            selectedDebtGroup = group;
        }));
        move.IsEnabled = groups.Length > 0;
        actions.Children.Add(move);
        panel.Children.Add(actions);if(state==DebtStatus.PendingConfirmation)panel.Children.Add(Theme.Label("No principal remains. Select Mark as Paid to confirm settlement.",12,Theme.Muted));if(debt.IsPaid)panel.Children.Add(Theme.Label("Reopen this debt before adding borrowing or changing records that leave principal outstanding.",12,Theme.Muted));
        var title=Theme.Label("Transaction History",16,bold:true);title.Margin=new Thickness(0,20,0,8);panel.Children.Add(title);
        foreach(var entry in doc.DebtEntries(debt.Id).OrderByDescending(e=>e.Item.Date).ThenByDescending(e=>doc.Transactions.IndexOf(e.Item))){
            var tx=entry.Item;
            var row=new DockPanel{Margin=new Thickness(0,8,0,8)};var edit=Theme.ActionButton("Edit",()=>DebtTransaction(tx.Kind,tx));edit.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(edit,Dock.Right);row.Children.Add(edit);
            var originLabel=tx.Kind==TransactionKind.Transfer?"Borrowed via Transfer":self?"Expense / Self Debt":"Borrowed";
            var text=Ui.Stack();text.Children.Add(Theme.Label(Display.Date(tx.Date)+" · "+(entry.AddsPrincipal?originLabel:"Payment")+" · "+doc.BankName(tx.BankId)+(tx.ToBankId is Guid to?" → "+doc.BankName(to):""),13,bold:true));text.Children.Add(Theme.Label(Display.Money(entry.Amount)+"    Admin Fee: "+Display.Money(tx.Fee),13));text.Children.Add(Theme.Label((tx.Kind==TransactionKind.Transfer?doc.AllocationSummary(tx):doc.CategoryName(tx.CategoryId))+(tx.Description.Length>0?" · "+tx.Description:""),12,Theme.Muted));row.Children.Add(text);panel.Children.Add(new Border{Child=row,BorderBrush=Theme.Rule,BorderThickness=new Thickness(0,0,0,1)});
        }
        var remove=Theme.ActionButton("Remove Tracking",()=>DebtAction("remove"));remove.HorizontalAlignment=HorizontalAlignment.Left;remove.Margin=new Thickness(0,16,0,0);panel.Children.Add(remove);return panel;
    }
    private void DebtTransaction(TransactionKind kind,Transaction? existing=null,Guid? preset=null,bool newDebt=false)
    {
        if(session.Document.Banks.Count<(kind==TransactionKind.Transfer?2:1)){ConfirmDialog.Alert(DetailOwner,"Add a bank first",kind==TransactionKind.Transfer?"A transfer needs two different bank accounts. Add accounts under Data → Accounts.":"Add your account under Data → Accounts before recording a debt transaction.");return;}
        if(new TransactionDialog(DetailOwner,session,kind,existing,preset,newDebt).ShowDialog()==true){var item=existing is null?session.Document.Transactions.Last():session.Document.Transactions.Single(t=>t.Id==existing.Id);selectedDebt=session.Document.RelatedDebtIds(item).Select(id=>(Guid?)id).FirstOrDefault()??selectedDebt;selectedDebtGroup=null;selectedBank=null;page="Debt";}
        Render();
    }
    private void DebtAction(string action)
    {
        try{
            if(action=="new"){
                var dialog=new FormWindow(DetailOwner,"New Debt","Start Self Debt from an Expense or Transfer, or record Income from a lender.","Continue");var type=new ComboBox{ItemsSource=new[]{"Self Debt from Expense","Self Debt from Transfer","Debt to a lender"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex switch{0=>TransactionKind.Expense,1=>TransactionKind.Transfer,_=>TransactionKind.Income},newDebt:true);return;
            }var debt=ActiveDebt;if(debt is null)return;
            if(action=="edit"){new DebtDetailsDialog(DetailOwner,session,debt).ShowDialog();Render();return;}
            if(action is "borrow" or "payment"){
                FinanceDocument.Require(!debt.IsPaid,"Reopen Debt before adding a transaction.");
                if(debt.Kind==DebtKind.Self&&action=="borrow"){
                    var dialog=new FormWindow(DetailOwner,"Add Self Debt","Use the original account for an Expense or a borrowing Transfer.","Continue");var type=new ComboBox{ItemsSource=session.Document.Banks.Count>1?new[]{"Expense from original account","Transfer from original account"}:new[]{"Expense from original account"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                    if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex==0?TransactionKind.Expense:TransactionKind.Transfer,preset:debt.Id,newDebt:type.SelectedIndex==1);return;
                }
                if(debt.Kind==DebtKind.Self&&action=="payment"){
                    var dialog=new FormWindow(DetailOwner,"Repay Self Debt","Mark Income received in the original account, or link a transfer Purpose allocation. No extra bank transaction is created by tracking.","Continue");
                    var type=new ComboBox{ItemsSource=session.Document.Banks.Count>1?new[]{"Income to original account","Transfer to original account"}:new[]{"Income to original account"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                    if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex==0?TransactionKind.Income:TransactionKind.Transfer,preset:debt.Id);return;
                }
                DebtTransaction(action=="borrow"?(debt.Kind==DebtKind.Self?TransactionKind.Expense:TransactionKind.Income):TransactionKind.Expense,preset:debt.Id);return;
            }
            var(title,message,button)=action switch{
                "paid"=>("Mark as Paid?","Confirm that "+debt.Name+" is fully settled? No bank transaction will be created.","Mark as Paid"),
                "reopen"=>("Reopen Debt?","Reopen "+debt.Name+" for additional borrowing or record corrections? Existing transactions are retained.","Reopen Debt"),
                _=>("Remove Debt Tracking?","Remove "+debt.Name+" and detach all of its debt links? All bank transactions, dates, amounts, categories and fees will be retained.","Remove Tracking")};
            if(!ConfirmDialog.Confirm(DetailOwner,title,message,button))return;
            session.Apply(action=="remove"?DebtService.RemoveTracking(session.Document,debt.Id):DebtService.SetPaid(session.Document,debt.Id,action=="paid"));Render();
        }catch(InvalidDataException e){ConfirmDialog.Alert(DetailOwner,"Debt",e.Message);Render();}
    }
}

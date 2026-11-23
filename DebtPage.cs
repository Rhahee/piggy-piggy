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
    private ScrollViewer? debtDetailScroll,debtListScroll;
    private readonly Dictionary<Guid,double> debtOffsets=[];
    private double debtListOffset;
    private Debt? ActiveDebt => session.Document.Debts.FirstOrDefault(d=>d.Id==selectedDebt);
    private List<Debt> VisibleDebts() => session.Document.Debts.Where(d=>selectedBank is null||session.Document.Transactions.Any(t=>session.Document.RelatedDebtIds(t).Contains(d.Id)&&(t.BankId==selectedBank||t.ToBankId==selectedBank))).ToList();
    private void ShowDebt(){page="Debt";Render();}
    private void CaptureDebtViewport()
    {
        if(debtDetailScroll?.IsLoaded==true&&renderedDebt is Guid id)debtOffsets[id]=debtDetailScroll.VerticalOffset;
        if(debtListScroll?.IsLoaded==true)debtListOffset=debtListScroll.VerticalOffset;debtDetailScroll=null;debtListScroll=null;
    }
    private void ResetDebtView(){selectedDebt=null;renderedDebt=null;debtDetailScroll=null;debtListScroll=null;debtOffsets.Clear();debtListOffset=0;}
    private UIElement DebtPage()
    {
        var doc=session.Document;var debts=VisibleDebts();var root=new DockPanel{Background=Theme.Background};
        var heading=Theme.SectionHeader("Debt","New Debt",()=>DebtAction("new"),"As of "+Display.Date(DateOnly.FromDateTime(DateTime.Today)));DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        var summary=new Grid();var values=new[]{("Principal",debts.Sum(d=>doc.DebtAmounts(d.Id).Borrowed)),("Principal Paid",debts.Sum(d=>doc.DebtAmounts(d.Id).Paid)),("Remaining Debt",debts.Sum(d=>doc.DebtAmounts(d.Id).Remaining))};
        for(var i=0;i<values.Length;i++){summary.ColumnDefinitions.Add(new ColumnDefinition());var panel=Ui.Stack();panel.Children.Add(Theme.Label(values[i].Item1,12,Theme.Muted));panel.Children.Add(Theme.Label(Display.Money(values[i].Item2),18,i==2?Theme.Accent:Theme.Text));var cell=new Border{Child=panel,Padding=new Thickness(12),BorderBrush=Theme.Rule,BorderThickness=new Thickness(0,0,i<2?1:0,0)};Grid.SetColumn(cell,i);summary.Children.Add(cell);}
        var totalsCard=Theme.Card(summary,new Thickness(4));totalsCard.Margin=new Thickness(12,8,12,0);DockPanel.SetDock(totalsCard,Dock.Top);root.Children.Add(totalsCard);
        var hint=Theme.Label(selectedBank is null?"Income and Expenses include the original transactions. Admin fees do not change debt principal.":"Account filter shows related debts. Each debt total includes all its linked accounts.",12,Theme.Muted);hint.Margin=new Thickness(12,8,12,8);DockPanel.SetDock(hint,Dock.Top);root.Children.Add(hint);
        if(debts.Count==0){var empty=Ui.Stack();empty.Children.Add(Theme.Label(doc.Debts.Count==0?"Track a debt":"No debts related to this account",20,bold:true));empty.Children.Add(Theme.Label("Use New Debt, or edit an existing Expense to track Self Debt. Income can track borrowing from a lender. Tracking does not create additional bank movements.",14,Theme.Muted));var card=Theme.Card(empty,new Thickness(20));card.VerticalAlignment=VerticalAlignment.Top;card.Margin=new Thickness(12);root.Children.Add(card);return root;}
        if(!debts.Any(d=>d.Id==selectedDebt))selectedDebt=debts[0].Id;var changed=renderedDebt!=selectedDebt;renderedDebt=selectedDebt;
        var split=new Grid{Margin=new Thickness(12)};split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(248),MinWidth=184});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=380});
        Ui.ConstrainSplitList(root,split);
        var list=Ui.Stack();FrameworkElement? selectedButton=null;
        foreach(var debt in debts){
            var body=Ui.Stack();body.Children.Add(Theme.Label(debt.Name,14,bold:true));body.Children.Add(Theme.Label(debt.Kind==DebtKind.Self?"Self Debt · "+doc.BankName(debt.SelfBankId!.Value):debt.Lender,12,Theme.Muted));body.Children.Add(Theme.Label(DebtService.StatusName(doc.DebtState(debt.Id))+(doc.DebtOverdue(debt.Id)?" · Overdue":""),12,doc.DebtOverdue(debt.Id)?Theme.Error:Theme.Muted));body.Children.Add(Theme.Label(Display.Money(doc.DebtAmounts(debt.Id).Remaining),14,Theme.Accent));
            var button=Ui.Button("",()=>{selectedDebt=debt.Id;Render();});button.Content=body;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,4);button.Padding=new Thickness(12,10,12,10);Theme.StyleButton(button,selected:debt.Id==selectedDebt);if(debt.Id==selectedDebt)selectedButton=button;
            list.Children.Add(button);
        }
        var listScroll=new ScrollViewer{Content=list,Background=Theme.Surface,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};debtListScroll=listScroll;split.Children.Add(Theme.Card(listScroll,new Thickness(8)));var divider=Theme.Splitter();Grid.SetColumn(divider,1);split.Children.Add(divider);
        var detail=new ScrollViewer{Content=DebtDetail(ActiveDebt!),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};debtDetailScroll=detail;var id=selectedDebt!.Value;
        detail.Loaded+=(_,_)=>Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{if(!ReferenceEquals(debtDetailScroll,detail)||!detail.IsLoaded)return;listScroll.ScrollToVerticalOffset(debtListOffset);if(changed)selectedButton?.BringIntoView();detail.ScrollToVerticalOffset(debtOffsets.GetValueOrDefault(id));}));
        var detailCard=Theme.Card(detail,new Thickness(16,12,12,12));Grid.SetColumn(detailCard,2);split.Children.Add(detailCard);root.Children.Add(split);return root;
    }
    private UIElement DebtDetail(Debt debt)
    {
        var doc=session.Document;var totals=doc.DebtAmounts(debt.Id);var state=doc.DebtState(debt.Id);var panel=Ui.Stack();
        var heading=new DockPanel();var status=Theme.Label(DebtService.StatusName(state)+(doc.DebtOverdue(debt.Id)?" · Overdue":""),12,doc.DebtOverdue(debt.Id)?Theme.Error:Theme.Muted);DockPanel.SetDock(status,Dock.Right);heading.Children.Add(status);heading.Children.Add(Theme.Label(debt.Name,22,bold:true));panel.Children.Add(heading);
        var self=debt.Kind==DebtKind.Self;
        var info=Theme.Label((self?"Self Debt · Return to: "+doc.BankName(debt.SelfBankId!.Value):"Lender: "+debt.Lender)+"\nDue Date: "+(debt.DueDate is DateOnly due?Display.Date(due):"Not set"),13,Theme.Muted);info.Margin=new Thickness(0,8,0,16);panel.Children.Add(info);
        panel.Children.Add(Theme.Label("Remaining Principal",12,Theme.Muted));panel.Children.Add(Theme.Label(Display.Money(totals.Remaining),24,Theme.Accent));panel.Children.Add(Theme.Label((self?"Funds to Restore: ":"Borrowed: ")+Display.Money(totals.Borrowed)+"    Principal Paid: "+Display.Money(totals.Paid),13));
        var actions=new WrapPanel{Margin=new Thickness(0,16,0,8)};
        void Add(string label,string action,bool enabled=true,bool primary=false){var button=Theme.ActionButton(label,()=>DebtAction(action),primary);button.IsEnabled=enabled;button.Margin=new Thickness(0,0,4,4);actions.Children.Add(button);}
        Add(self?"Add Debt":"Add Borrowing","borrow",!debt.IsPaid);Add("Record Payment","payment",!debt.IsPaid&&totals.Remaining>0,true);Add("Edit Details","edit");
        if(debt.IsPaid)Add("Reopen Debt","reopen");else Add("Mark as Paid","paid",totals.Remaining==0);
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
        if(session.Document.Banks.Count<(kind==TransactionKind.Transfer?2:1)){ConfirmDialog.Alert(this,"Add a bank first",kind==TransactionKind.Transfer?"A transfer needs two different bank accounts. Add accounts under Data → Accounts.":"Add your account under Data → Accounts before recording a debt transaction.");return;}
        if(new TransactionDialog(this,session,kind,existing,preset,newDebt).ShowDialog()==true){var item=existing is null?session.Document.Transactions.Last():session.Document.Transactions.Single(t=>t.Id==existing.Id);selectedDebt=session.Document.RelatedDebtIds(item).Select(id=>(Guid?)id).FirstOrDefault()??selectedDebt;selectedBank=null;page="Debt";}
        Render();
    }
    private void DebtAction(string action)
    {
        try{
            if(action=="new"){
                var dialog=new FormWindow(this,"New Debt","Start Self Debt from an Expense or Transfer, or record Income from a lender.","Continue");var type=new ComboBox{ItemsSource=new[]{"Self Debt from Expense","Self Debt from Transfer","Debt to a lender"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex switch{0=>TransactionKind.Expense,1=>TransactionKind.Transfer,_=>TransactionKind.Income},newDebt:true);return;
            }var debt=ActiveDebt;if(debt is null)return;
            if(action=="edit"){new DebtDetailsDialog(this,session,debt).ShowDialog();Render();return;}
            if(action is "borrow" or "payment"){
                FinanceDocument.Require(!debt.IsPaid,"Reopen Debt before adding a transaction.");
                if(debt.Kind==DebtKind.Self&&action=="borrow"){
                    var dialog=new FormWindow(this,"Add Self Debt","Use the original account for an Expense or a borrowing Transfer.","Continue");var type=new ComboBox{ItemsSource=session.Document.Banks.Count>1?new[]{"Expense from original account","Transfer from original account"}:new[]{"Expense from original account"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                    if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex==0?TransactionKind.Expense:TransactionKind.Transfer,preset:debt.Id,newDebt:type.SelectedIndex==1);return;
                }
                if(debt.Kind==DebtKind.Self&&action=="payment"){
                    var dialog=new FormWindow(this,"Repay Self Debt","Mark Income received in the original account, or link a transfer Purpose allocation. No extra bank transaction is created by tracking.","Continue");
                    var type=new ComboBox{ItemsSource=session.Document.Banks.Count>1?new[]{"Income to original account","Transfer to original account"}:new[]{"Income to original account"},SelectedIndex=0};dialog.AddFields(type);dialog.OnSubmit(()=>{});
                    if(dialog.ShowDialog()==true)DebtTransaction(type.SelectedIndex==0?TransactionKind.Income:TransactionKind.Transfer,preset:debt.Id);return;
                }
                DebtTransaction(action=="borrow"?(debt.Kind==DebtKind.Self?TransactionKind.Expense:TransactionKind.Income):TransactionKind.Expense,preset:debt.Id);return;
            }
            var(title,message,button)=action switch{
                "paid"=>("Mark as Paid?","Confirm that "+debt.Name+" is fully settled? No bank transaction will be created.","Mark as Paid"),
                "reopen"=>("Reopen Debt?","Reopen "+debt.Name+" for additional borrowing or record corrections? Existing transactions are retained.","Reopen Debt"),
                _=>("Remove Debt Tracking?","Remove "+debt.Name+" and detach all of its debt links? All bank transactions, dates, amounts, categories and fees will be retained.","Remove Tracking")};
            if(!ConfirmDialog.Confirm(this,title,message,button))return;
            session.Apply(action=="remove"?DebtService.RemoveTracking(session.Document,debt.Id):DebtService.SetPaid(session.Document,debt.Id,action=="paid"));Render();
        }catch(InvalidDataException e){ConfirmDialog.Alert(this,"Debt",e.Message);Render();}
    }
}

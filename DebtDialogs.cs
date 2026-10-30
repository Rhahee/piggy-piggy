using System.IO;
using System.Windows;
using System.Windows.Controls;
using MyPiggyBank.Core;

namespace MyPiggyBank;

internal sealed record DebtTrackingResult(Guid? DebtId=null,Debt? NewDebt=null,SelfDebtPayment? SelfPayment=null);
internal sealed class DebtTrackingEditor : StackPanel
{
    private sealed record Choice(Guid? Id,string Name);
    private readonly CheckBox track=new(){Content="Track as debt",Margin=new Thickness(0,0,0,8)};
    private readonly StackPanel details=Ui.Stack(),newFields=Ui.Stack();
    private readonly ComboBox mode=new(),choice=new(){DisplayMemberPath="Name"};
    private readonly TextBox name=new(){MaxLength=100},lender=new(){MaxLength=100},due=FormWindow.DateInput(DateOnly.FromDateTime(DateTime.Today));
    private readonly TextBox repayment=FormWindow.MoneyInput();
    private readonly TextBlock preview=Ui.Text("",12,Ui.Muted),hint=Ui.Text("",12,Ui.Muted);
    private readonly FrameworkElement lenderRow,repaymentRow;
    private readonly DocumentSession session;
    private readonly TransactionKind kind;
    private readonly Transaction? existing;
    private readonly Func<DateOnly> readDate;
    private readonly Func<Guid?> readBank;
    private readonly Func<decimal> readAmount,readFee;
    private readonly Func<bool> afterFee;
    private bool changing;
    private bool Self => mode.SelectedIndex==0;
    private bool IsRepayment => Self&&kind==TransactionKind.Income;
    public DebtTrackingEditor(DocumentSession session,TransactionKind kind,Transaction? existing,Func<DateOnly> readDate,Func<Guid?> readBank,Func<decimal> readAmount,Func<decimal> readFee,Func<bool> afterFee,Action changed,Guid? preset,bool trackNew)
    {
        this.session=session;this.kind=kind;this.existing=existing;this.readDate=readDate;this.readBank=readBank;this.readAmount=readAmount;this.readFee=readFee;this.afterFee=afterFee;
        due.Text="";Children.Add(track);Children.Add(details);
        mode.ItemsSource=kind==TransactionKind.Income?new[]{"Repay Self Debt","Borrow from lender"}:new[]{"Self Debt — restore this account","Pay lender"};
        var selected=preset??existing?.DebtId??existing?.SelfDebtPayment?.DebtId;
        mode.SelectedIndex=selected is Guid id?session.Document.Debts.First(d=>d.Id==id).Kind==DebtKind.Self?0:1:kind==TransactionKind.Expense?0:1;
        Row(details,"Action *",mode);Row(details,"Debt *",choice);Row(newFields,"Debt Name *",name);lenderRow=Row(newFields,"Lender *",lender);Row(newFields,"Due Date",FormWindow.DatePickerInput(due));details.Children.Add(newFields);
        repayment.Text=existing?.SelfDebtPayment is {} p?p.Amount.ToString("N2",Display.Culture):"";repaymentRow=Row(details,"Repayment\nAmount (Rp) *",repayment);
        preview.Margin=new Thickness(0,8,0,0);details.Children.Add(preview);hint.Margin=new Thickness(0,8,0,0);details.Children.Add(hint);
        void Changed(){Refresh();changed();}
        track.Checked+=(_,_)=>Changed();track.Unchecked+=(_,_)=>Changed();choice.SelectionChanged+=(_,_)=>{if(!changing)Changed();};repayment.TextChanged+=(_,_)=>Changed();
        mode.SelectionChanged+=(_,_)=>{LoadChoices(null);Changed();};LoadChoices(selected);
        track.IsChecked=existing?.DebtId is not null||existing?.SelfDebtPayment is not null||preset is not null||trackNew;Refresh();
    }
    private void LoadChoices(Guid? selected)
    {
        changing=true;
        try{
            var options=session.Document.Debts.Where(d=>d.Kind==(Self?DebtKind.Self:DebtKind.External)&&(!d.IsPaid||d.Id==existing?.DebtId||d.Id==existing?.SelfDebtPayment?.DebtId)).OrderBy(d=>d.Name).Select(d=>new Choice(d.Id,session.Document.DebtLabel(d.Id))).ToList();
            var creates=Self?kind==TransactionKind.Expense:kind==TransactionKind.Income;if(creates)options.Insert(0,new(null,Self?"New Self Debt":"New Debt"));choice.ItemsSource=options;
            choice.SelectedItem=selected is Guid id?options.FirstOrDefault(c=>c.Id==id):creates?options.First():null;
        }finally{changing=false;}
    }
    private static FrameworkElement Row(Panel parent,string label,UIElement input)
    {
        var row=new Grid{Margin=new Thickness(0,4,0,4)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(112)});row.ColumnDefinitions.Add(new ColumnDefinition());
        var text=Ui.Text(label,13);text.Margin=new Thickness(0,0,8,0);row.Children.Add(text);Grid.SetColumn(input,1);row.Children.Add(input);parent.Children.Add(row);return row;
    }
    public void Refresh()
    {
        details.Visibility=track.IsChecked==true?Visibility.Visible:Visibility.Collapsed;newFields.Visibility=choice.SelectedItem is Choice{Id:null}?Visibility.Visible:Visibility.Collapsed;
        lenderRow.Visibility=Self?Visibility.Collapsed:Visibility.Visible;repaymentRow.Visibility=IsRepayment?Visibility.Visible:Visibility.Collapsed;
        hint.Text=Self?"Return funds to the original account. Only the marked repayment reduces Self Debt; admin fees are separate. Reports and bank movements stay unchanged.":"Admin Fee is separate from principal. Income/Expenses reports stay unchanged. Reopen Paid debts before adding borrowing.";
        if(track.IsChecked!=true)return;
        try{
            var date=readDate();var amount=readAmount();var principal=IsRepayment?MoneyEditing.Read(repayment.Text,true):kind==TransactionKind.Income&&afterFee()?amount+readFee():amount;
            var adds=Self?kind==TransactionKind.Expense:kind==TransactionKind.Income;
            if(choice.SelectedItem is not Choice selected){preview.Text="Choose the debt being paid.";return;}
            if(selected.Id is not Guid id){preview.Text=(Self?"Self Debt principal: ":"Borrowed principal: ")+Display.Money(principal)+(Self&&readBank() is Guid bank?"\nReturn to: "+session.Document.BankName(bank):"");return;}
            var debt=session.Document.Debts.First(d=>d.Id==id);var doc=session.Document.Copy();if(existing is not null)doc.Transactions.RemoveAll(t=>t.Id==existing.Id);
            preview.Text=$"As of {Display.Date(date)}\nRemaining after this transaction: {Display.Money(doc.DebtAmounts(id,date).Remaining+(adds?principal:-principal))}";
            if(Self)preview.Text+="\nReturn to: "+doc.BankName(debt.SelfBankId!.Value);
        }catch(Exception e)when(e is InvalidDataException or FormatException or OverflowException){preview.Text=e.Message;}
    }
    public DebtTrackingResult Read()
    {
        if(track.IsChecked!=true)return new();
        FinanceDocument.Require(choice.SelectedItem is Choice,"Choose a debt.");var selected=(Choice)choice.SelectedItem;
        if(selected.Id is Guid id)return IsRepayment?new(SelfPayment:new(id,MoneyEditing.Read(repayment.Text))):new(DebtId:id);
        FinanceDocument.Require((Self&&kind==TransactionKind.Expense)||(!Self&&kind==TransactionKind.Income),"Choose an existing debt for a payment.");
        DateOnly? date=null;if(!string.IsNullOrWhiteSpace(due.Text)){FinanceDocument.Require(DateOnly.TryParseExact(due.Text.Trim(),"MM/dd/yyyy",Display.Culture,System.Globalization.DateTimeStyles.None,out var parsed),"Enter a valid due date in MM/DD/YYYY format.");date=parsed;}
        var debt=new Debt(Guid.NewGuid(),name.Text.Trim(),Self?"":lender.Text.Trim(),date,Kind:Self?DebtKind.Self:DebtKind.External,SelfBankId:Self?readBank():null);return new(debt.Id,debt);
    }
}

internal sealed class DebtDetailsDialog : FormWindow
{
    public DebtDetailsDialog(Window owner,DocumentSession session,Debt debt):base(owner,"Edit Debt","Fields marked * are required. Details can be edited in every status.","Save Changes",560)
    {
        var name=new TextBox{Text=debt.Name,MaxLength=100};var lender=new TextBox{Text=debt.Lender,MaxLength=100};var due=DateInput(debt.DueDate??DateOnly.FromDateTime(DateTime.Today));if(debt.DueDate is null)due.Text="";
        Field("Debt Name *",name);if(debt.Kind==DebtKind.External)Field("Lender *",lender);else Field("Return to",Ui.Text(session.Document.BankName(debt.SelfBankId!.Value)));DateField("Due Date",due);
        OnSubmit(()=>session.Apply(DebtService.EditDetails(session.Document,debt.Id,name.Text.Trim(),lender.Text.Trim(),string.IsNullOrWhiteSpace(due.Text)?null:ReadDate(due))));
    }
}

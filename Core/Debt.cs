namespace MyPiggyBank.Core;

public enum DebtKind { External, Self }
public sealed record Debt(Guid Id,string Name,string Lender,DateOnly? DueDate,bool IsPaid=false,DebtKind Kind=DebtKind.External,Guid? SelfBankId=null);
public sealed record SelfDebtPayment(Guid DebtId,decimal Amount);
public enum DebtStatus { Open,PartiallyPaid,PendingConfirmation,Paid }
public sealed record DebtTotals(decimal Borrowed,decimal Paid) { public decimal Remaining => Borrowed-Paid; }
public sealed record DebtEntry(Transaction Item,bool AddsPrincipal,decimal Amount);

public sealed partial class FinanceDocument
{
    public List<Debt> Debts { get; set; }=[];
    public string DebtLabel(Guid? id)
    {
        if(id is not Guid value)return "—";var debt=Debts.First(d=>d.Id==value);
        return debt.Name+" · "+(debt.Kind==DebtKind.Self?"Self Debt · "+BankName(debt.SelfBankId!.Value):debt.Lender);
    }
    public IEnumerable<Guid> RelatedDebtIds(Transaction transaction)
    {
        if(transaction.DebtId is Guid id)yield return id;
        if(transaction.SelfDebtPayment is {} payment)yield return payment.DebtId;
        foreach(var allocation in transaction.Allocations??[])if(allocation.SelfDebtId is Guid self)yield return self;
    }
    public string TransactionDebtLabel(Transaction transaction) => string.Join("; ",RelatedDebtIds(transaction).Distinct().Select(id=>DebtLabel(id))) is {Length:>0} label?label:"—";
    public IEnumerable<DebtEntry> DebtEntries(Guid id)
    {
        var debt=Debts.First(d=>d.Id==id);
        foreach(var tx in Transactions){
            if(tx.DebtId==id)yield return new(tx,debt.Kind==DebtKind.Self?tx.Kind==TransactionKind.Expense:tx.Kind==TransactionKind.Income,tx.Kind==TransactionKind.Income?tx.GrossIncome:tx.Amount);
            if(tx.SelfDebtPayment is {} payment&&payment.DebtId==id)yield return new(tx,false,payment.Amount);
            foreach(var action in Enum.GetValues<SelfDebtAction>()){
                var allocated=(tx.Allocations??[]).Where(a=>a.SelfDebtId==id&&a.DebtAction==action).Sum(a=>a.Amount);
                if(allocated>0)yield return new(tx,action==SelfDebtAction.Borrow,allocated);
            }
        }
    }
    public DebtTotals DebtAmounts(Guid id,DateOnly? through=null)
    {
        var date=through??DateOnly.FromDateTime(DateTime.Today);var entries=DebtEntries(id).Where(e=>e.Item.Date<=date).ToList();
        return new(entries.Where(e=>e.AddsPrincipal).Sum(e=>e.Amount),entries.Where(e=>!e.AddsPrincipal).Sum(e=>e.Amount));
    }
    public DebtStatus DebtState(Guid id)
    {
        var debt=Debts.First(d=>d.Id==id);var amounts=DebtAmounts(id);
        return debt.IsPaid?DebtStatus.Paid:amounts.Remaining==0?DebtStatus.PendingConfirmation:amounts.Paid>0?DebtStatus.PartiallyPaid:DebtStatus.Open;
    }
    public bool DebtOverdue(Guid id) => Debts.First(d=>d.Id==id).DueDate is DateOnly due&&due<DateOnly.FromDateTime(DateTime.Today)&&DebtAmounts(id).Remaining>0;
    private void ValidateDebts()
    {
        Require(Debts is not null&&Debts.All(d=>d is not null),"The debt list is incomplete.");
        Require(Version>=7||(Debts.Count==0&&Transactions.All(t=>t.DebtId is null)),"Debt tracking requires document format 7.");
        Require(Version>=8||(Debts.All(d=>d.Kind==DebtKind.External&&d.SelfBankId is null)&&Transactions.All(t=>t.SelfDebtPayment is null&&(t.Allocations??[]).All(a=>a.SelfDebtId is null))),"Self Debt requires document format 8.");
        Require(Version>=9||Transactions.All(t=>(t.Allocations??[]).All(a=>a.DebtAction==SelfDebtAction.Repay)),"Borrow as Self Debt requires document format 9.");
        Require(Debts.All(d=>d.Id!=Guid.Empty)&&Debts.Select(d=>d.Id).Distinct().Count()==Debts.Count,"Debt identifiers must be unique.");
        foreach(var debt in Debts){
            Require(Enum.IsDefined(debt.Kind),"Choose a valid debt type.");
            Require(!string.IsNullOrWhiteSpace(debt.Name)&&debt.Name==debt.Name.Trim()&&debt.Name.Length<=100,"Enter a debt name (up to 100 characters).");
            if(debt.Kind==DebtKind.External){
                Require(!string.IsNullOrWhiteSpace(debt.Lender)&&debt.Lender==debt.Lender.Trim()&&debt.Lender.Length<=100,"Enter a lender (up to 100 characters).");
                Require(debt.SelfBankId is null,"Only Self Debt has an original account.");
            }else{
                Require(debt.Lender=="","Self Debt does not have a lender.");
                Require(Banks.Any(b=>b.Id==debt.SelfBankId),"Choose the original Self Debt account.");
            }
            Require(debt.DueDate is null||debt.DueDate!=default(DateOnly),"Enter a valid due date or leave it blank.");
        }
        foreach(var transaction in Transactions){
            var ids=RelatedDebtIds(transaction).ToList();if(ids.Count==0)continue;
            Require(ids.All(id=>Debts.Any(d=>d.Id==id)),"Choose an existing debt.");
            Require(transaction.Date<=DateOnly.FromDateTime(DateTime.Today),"Debt transactions must be today or earlier.");
            Require(!IsWishlistTransaction(transaction.Id),"A wishlist-linked transaction cannot also track debt.");
            Require(transaction.DebtId is null||transaction.SelfDebtPayment is null,"One Income cannot borrow from a lender and repay Self Debt at the same time.");
            if(transaction.DebtId is Guid id){
                var debt=Debts.First(d=>d.Id==id);
                if(debt.Kind==DebtKind.External)Require(transaction.Kind is TransactionKind.Income or TransactionKind.Expense,"Transfers cannot borrow from or repay an external lender.");
                else Require(transaction.Kind==TransactionKind.Expense&&transaction.BankId==debt.SelfBankId,"A Self Debt starts with an Expense from its original account. Repay using Income or a transfer allocation.");
            }
            if(transaction.SelfDebtPayment is {} payment){
                var debt=Debts.First(d=>d.Id==payment.DebtId);
                Require(transaction.Kind==TransactionKind.Income&&debt.Kind==DebtKind.Self,"Only Income can use a direct Self Debt repayment.");
                Require(transaction.BankId==debt.SelfBankId,$"Return {debt.Name} to its original account: {BankName(debt.SelfBankId!.Value)}.");
                Require(payment.Amount>0&&IsMoney(payment.Amount)&&payment.Amount<=transaction.NetIncome,"Self Debt repayment must be positive and cannot exceed net Income after fees.");
            }
            foreach(var allocation in (transaction.Allocations??[]).Where(a=>a.SelfDebtId is not null)){
                var debt=Debts.First(d=>d.Id==allocation.SelfDebtId);
                Require(transaction.Kind==TransactionKind.Transfer&&debt.Kind==DebtKind.Self,"Transfer debt allocations must link to Self Debt.");
                if(allocation.DebtAction==SelfDebtAction.Borrow)Require(transaction.BankId==debt.SelfBankId,$"Borrow {debt.Name} from its original account: {BankName(debt.SelfBankId!.Value)}.");
                else Require(transaction.ToBankId==debt.SelfBankId,$"Return {debt.Name} to its original account: {BankName(debt.SelfBankId!.Value)}.");
            }
        }
        foreach(var debt in Debts){
            var entries=DebtEntries(debt.Id).ToList();
            Require(entries.Any(e=>e.AddsPrincipal),$"{debt.Name} needs a recorded {(debt.Kind==DebtKind.Self?"original Expense or borrowing transfer":"borrowing")}. Use Remove Tracking before removing its last origin link.");
            decimal borrowed=0,paid=0;
            // Dates have no time component; same-day origins and repayments are evaluated together.
            foreach(var day in entries.GroupBy(e=>e.Item.Date).OrderBy(g=>g.Key)){
                borrowed+=day.Where(e=>e.AddsPrincipal).Sum(e=>e.Amount);paid+=day.Where(e=>!e.AddsPrincipal).Sum(e=>e.Amount);
                Require(IsMoney(borrowed)&&IsMoney(paid),"Debt totals exceed the supported amount range.");
                Require(paid<=borrowed,$"Payments exceed the remaining principal for {debt.Name} on {Display.Date(day.Key)}. Check original and later payment records.");
            }
            Require(!debt.IsPaid||borrowed==paid,$"{debt.Name} is marked Paid. Reopen Debt before making a change that leaves principal outstanding.");
        }
    }
}

public static class DebtService
{
    public static FinanceDocument SaveTransaction(FinanceDocument source,Transaction transaction,Debt? newDebt=null,IReadOnlyList<Debt>? additionalDebts=null)
    {
        var doc=source.Copy();
        foreach(var debt in (newDebt is null?Array.Empty<Debt>():new[]{newDebt}).Concat(additionalDebts??[])){
            var origin=transaction.DebtId==debt.Id&&transaction.Kind==(debt.Kind==DebtKind.Self?TransactionKind.Expense:TransactionKind.Income);
            var transfer=debt.Kind==DebtKind.Self&&transaction.Kind==TransactionKind.Transfer&&(transaction.Allocations??[]).Any(a=>a.SelfDebtId==debt.Id&&a.DebtAction==SelfDebtAction.Borrow);
            FinanceDocument.Require((origin||transfer)&&!debt.IsPaid,"A new debt must start with its original transaction.");doc.Debts.Add(debt);
        }
        var index=doc.Transactions.FindIndex(t=>t.Id==transaction.Id);if(index<0)doc.Transactions.Add(transaction);else doc.Transactions[index]=transaction;
        doc.Validate();return doc;
    }
    public static FinanceDocument EditDetails(FinanceDocument source,Guid id,string name,string lender,DateOnly? dueDate)
    {
        var doc=source.Copy();var index=doc.Debts.FindIndex(d=>d.Id==id);FinanceDocument.Require(index>=0,"Choose an existing debt.");
        doc.Debts[index]=doc.Debts[index] with{Name=name,Lender=lender,DueDate=dueDate};doc.Validate();return doc;
    }
    public static FinanceDocument SetPaid(FinanceDocument source,Guid id,bool paid)
    {
        var doc=source.Copy();var index=doc.Debts.FindIndex(d=>d.Id==id);FinanceDocument.Require(index>=0,"Choose an existing debt.");
        FinanceDocument.Require(!paid||doc.DebtAmounts(id).Remaining==0,"Repay the remaining principal before Mark as Paid.");
        doc.Debts[index]=doc.Debts[index] with{IsPaid=paid};doc.Validate();return doc;
    }
    public static FinanceDocument RemoveTracking(FinanceDocument source,Guid id)
    {
        FinanceDocument.Require(source.Debts.Any(d=>d.Id==id),"Choose an existing debt.");var doc=source.Copy();doc.Debts.RemoveAll(d=>d.Id==id);
        doc.Transactions=doc.Transactions.Select(t=>t with{DebtId=t.DebtId==id?null:t.DebtId,SelfDebtPayment=t.SelfDebtPayment?.DebtId==id?null:t.SelfDebtPayment,Allocations=t.Allocations?.Select(a=>a.SelfDebtId==id?a with{SelfDebtId=null,DebtAction=SelfDebtAction.Repay}:a).GroupBy(a=>(a.PurposeId,a.SelfDebtId,a.DebtAction)).Select(g=>g.First() with{Amount=g.Sum(a=>a.Amount)}).ToArray()}).ToList();doc.Validate();return doc;
    }
    public static string StatusName(DebtStatus status) => status switch{DebtStatus.PartiallyPaid=>"Partially Paid",DebtStatus.PendingConfirmation=>"Pending Confirmation",_=>status.ToString()};
}

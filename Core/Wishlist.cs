namespace MyPiggyBank.Core;

public enum WishlistStatus { Planned, Purchased, Closed, Cancelled }
public enum WishlistPriority { Low, Normal, High }
public enum WishlistEventKind { Created, Edited, Allocated, Released, Transferred, Reassigned, Purchased, PurchaseCorrected, Cancelled, Closed }
public sealed record WishlistItem(Guid Id, string Name, decimal TargetAmount, DateOnly? TargetDate, WishlistPriority Priority, string Notes);
public sealed record WishlistEvent(Guid Id, Guid WishlistId, DateOnly Date, WishlistEventKind Kind, Guid? FromBankId, Guid? ToBankId, decimal Amount, Guid? TransactionId, decimal Fee, string Description);

public sealed partial class FinanceDocument
{
    public List<WishlistItem> Wishlists { get; set; } = [];
    public List<WishlistEvent> WishlistEvents { get; set; } = [];
    public WishlistStatus WishlistState(Guid id)
    {
        var last = WishlistEvents.LastOrDefault(e => e.WishlistId == id && e.Kind is WishlistEventKind.Purchased or WishlistEventKind.PurchaseCorrected or WishlistEventKind.Cancelled or WishlistEventKind.Closed);
        return last?.Kind switch { WishlistEventKind.Purchased => WishlistStatus.Purchased, WishlistEventKind.Closed => WishlistStatus.Closed, WishlistEventKind.Cancelled => WishlistStatus.Cancelled, _ => WishlistStatus.Planned };
    }
    public bool IsWishlistTransaction(Guid id) => WishlistEvents.Any(e => e.TransactionId == id && e.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred);
    public decimal WishlistAllocated(Guid id, Guid? bank = null, DateOnly? through = null)
    {
        var date = through ?? DateOnly.FromDateTime(DateTime.Today);
        return WishlistEvents.Where(e => e.WishlistId == id && e.Date <= date).Sum(e => AllocationChanges(e).Where(c => bank is null || c.Bank == bank).Sum(c => c.Delta));
    }
    public decimal WishlistReserved(Guid bank, DateOnly? through = null) => Wishlists.Sum(w => WishlistAllocated(w.Id,bank,through));
    public decimal AvailableBalance(Guid bank, DateOnly? through = null) { var date = through ?? DateOnly.FromDateTime(DateTime.Today); return Balance(bank,date) - WishlistReserved(bank,date); }
    internal static IEnumerable<(Guid Bank,decimal Delta)> AllocationChanges(WishlistEvent e)
    {
        if (e.Kind is WishlistEventKind.Released or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.Purchased) yield return (e.FromBankId!.Value,-e.Amount);
        if (e.Kind is WishlistEventKind.Allocated or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.PurchaseCorrected) yield return (e.ToBankId!.Value,e.Amount);
    }
    private void ValidateWishlists()
    {
        Require(Wishlists is not null && WishlistEvents is not null && Wishlists.All(w => w is not null) && WishlistEvents.All(e => e is not null),"The wishlist data is incomplete.");
        Require(Version >= 6 || (Wishlists.Count == 0 && WishlistEvents.Count == 0),"Wishlist data requires document format 6.");
        Require(Wishlists.All(w => w.Id != Guid.Empty) && Wishlists.Select(w => w.Id).Distinct().Count() == Wishlists.Count,"Wishlist identifiers must be unique.");
        foreach (var w in Wishlists)
        {
            Require(!string.IsNullOrWhiteSpace(w.Name) && w.Name == w.Name.Trim() && w.Name.Length <= 100,"Enter a wishlist name (up to 100 characters).");
            Require(w.TargetAmount > 0 && IsMoney(w.TargetAmount),"Target amount must be greater than zero, with at most two decimals.");
            Require(Enum.IsDefined(w.Priority) && (w.TargetDate is null || w.TargetDate != default(DateOnly)) && w.Notes is not null && w.Notes.Length <= 2000,"Enter a valid priority, target date and notes (up to 2,000 characters).");
        }
        Require(WishlistEvents.All(e => e.Id != Guid.Empty) && WishlistEvents.Select(e => e.Id).Distinct().Count() == WishlistEvents.Count,"Wishlist activity identifiers must be unique.");
        var reversed = WishlistEvents.Where(e => e.Kind == WishlistEventKind.PurchaseCorrected).Select(e => e.TransactionId).ToList();
        Require(reversed.Distinct().Count() == reversed.Count,"A purchase may only be corrected once.");
        foreach (var e in WishlistEvents)
        {
            Require(Enum.IsDefined(e.Kind),"Choose a valid wishlist activity.");
            Require(e.Amount>=0 && IsMoney(e.Amount) && e.Fee>=0 && IsMoney(e.Fee),"Wishlist amounts must be valid Rupiah values.");
            var debit=e.Kind is WishlistEventKind.Released or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.Purchased;
            var credit=e.Kind is WishlistEventKind.Allocated or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.PurchaseCorrected;
            Require(debit==e.FromBankId.HasValue && credit==e.ToBankId.HasValue,"Wishlist allocation account fields do not match the activity.");
        }
        var links = new HashSet<Guid>();
        foreach (var w in Wishlists)
        {
            var status = WishlistStatus.Planned; WishlistEvent? purchase = null; var created = false; var funds=new Dictionary<Guid,decimal>(); var lastFundsDate=DateOnly.MinValue;
            foreach (var e in WishlistEvents.Where(e => e.WishlistId == w.Id))
            {
                Require(created || e.Kind == WishlistEventKind.Created,"Wishlist history must begin with creation.");
                if(e.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited) { Require(e.Date>=lastFundsDate,"Wishlist funds activities must be in date order.");lastFundsDate=e.Date; }
                switch (e.Kind)
                {
                    case WishlistEventKind.Created: Require(!created,"A wishlist can only be created once."); created = true; break;
                    case WishlistEventKind.Edited: Require(status == WishlistStatus.Planned,"Only planned wishlists can be edited."); break;
                    case WishlistEventKind.Allocated:
                    case WishlistEventKind.Transferred:
                    case WishlistEventKind.Reassigned: Require(status == WishlistStatus.Planned,"Only planned wishlists can receive or move funds."); break;
                    case WishlistEventKind.Released: Require(status is WishlistStatus.Planned or WishlistStatus.Purchased,"This wishlist cannot release funds."); break;
                    case WishlistEventKind.Purchased: Require(status == WishlistStatus.Planned,"This wishlist has already been purchased or cancelled."); status = WishlistStatus.Purchased; purchase = e; break;
                    case WishlistEventKind.PurchaseCorrected:
                        Require(status is WishlistStatus.Purchased or WishlistStatus.Closed && purchase is not null,"There is no purchase to correct.");
                        Require(purchase!.TransactionId == e.TransactionId && purchase.Amount == e.Amount && purchase.FromBankId == e.ToBankId && purchase.Fee == e.Fee && e.Date >= purchase.Date,"The purchase correction must restore the exact recorded purchase allocation.");
                        Require(!Transactions.Any(t => t.Id == e.TransactionId),"The corrected purchase transaction must be reversed."); status = WishlistStatus.Planned; purchase = null; break;
                    case WishlistEventKind.Cancelled: Require(status == WishlistStatus.Planned && funds.Values.Sum() == 0,"Release every allocation before cancelling."); status = WishlistStatus.Cancelled; break;
                    case WishlistEventKind.Closed: Require(status == WishlistStatus.Purchased && funds.Values.Sum() == 0,"Purchase the item and release remaining allocation before closing."); status = WishlistStatus.Closed; break;
                }
                foreach(var change in AllocationChanges(e)) funds[change.Bank]=funds.GetValueOrDefault(change.Bank)+change.Delta;
                Require(funds.Values.All(v=>v>=0),"An activity exceeds the wishlist allocation available at that point in its history.");
            }
            Require(created,"The wishlist creation history is missing.");
        }
        foreach (var e in WishlistEvents)
        {
            Require(Wishlists.Any(w => w.Id == e.WishlistId) && Enum.IsDefined(e.Kind) && e.Date != default && e.Description is not null && e.Description.Length <= 2000,"Wishlist history contains an invalid entry.");
            Require(e.Amount >= 0 && IsMoney(e.Amount) && e.Fee >= 0 && IsMoney(e.Fee),"Wishlist amounts must be valid Rupiah values.");
            var debit = e.Kind is WishlistEventKind.Released or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.Purchased;
            var credit = e.Kind is WishlistEventKind.Allocated or WishlistEventKind.Transferred or WishlistEventKind.Reassigned or WishlistEventKind.PurchaseCorrected;
            Require(debit == e.FromBankId.HasValue && credit == e.ToBankId.HasValue,"Wishlist allocation account fields do not match the activity.");
            foreach (var id in new[] { e.FromBankId,e.ToBankId }.Where(id => id is not null)) Require(Banks.Any(b => b.Id == id && e.Date >= b.OpeningDate),"Wishlist activities require an existing bank and cannot predate its opening balance.");
            if (debit && credit) Require(e.FromBankId != e.ToBankId,"Choose two different accounts.");
            Require(debit || credit ? e.Amount > 0 : e.Amount == 0,"Enter a positive allocation amount.");
            var linked = e.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred;
            Require(linked || e.Kind == WishlistEventKind.PurchaseCorrected ? e.TransactionId is not null : e.TransactionId is null,"The wishlist transaction link is invalid.");
            Require(linked || e.Kind == WishlistEventKind.PurchaseCorrected || e.Fee == 0,"Only financial wishlist activities may carry admin fees.");
            if (linked)
            {
                Require(links.Add(e.TransactionId!.Value),"A transaction cannot fund two wishlist activities.");
                var transaction = Transactions.FirstOrDefault(t => t.Id == e.TransactionId);
                if (e.Kind == WishlistEventKind.Purchased && reversed.Contains(e.TransactionId)) Require(transaction is null,"A corrected purchase cannot remain in the ledger.");
                else Require(transaction is not null && transaction.Date == e.Date && transaction.Amount == e.Amount && transaction.Fee == e.Fee && transaction.BankId == e.FromBankId &&
                    (e.Kind == WishlistEventKind.Purchased ? transaction.Kind == TransactionKind.Expense : transaction.Kind == TransactionKind.Transfer && transaction.ToBankId == e.ToBankId),"This transaction is linked to a wishlist. Correct purchases from Wishlist; linked transfers cannot be edited or deleted independently.");
                Require(AvailableBalance(e.FromBankId!.Value,e.Date)>=0,$"Insufficient available balance in {BankName(e.FromBankId.Value)} on {Display.Date(e.Date)}. Wishlist funds are protected. Release or move the allocation first.");
            }
        }
        if (WishlistEvents.Any(e => e.Amount > 0)) ValidateAllocationTimeline();
    }
    private void ValidateAllocationTimeline()
    {
        var dates = new SortedDictionary<DateOnly,List<(Guid Bank,Guid? Wish,decimal Delta)>>();
        void Add(DateOnly date,Guid bank,Guid? wish,decimal delta) { if (!dates.TryGetValue(date,out var list)) dates[date] = list = []; list.Add((bank,wish,delta)); }
        foreach (var b in Banks) Add(b.OpeningDate,b.Id,null,b.OpeningBalance);
        foreach (var t in Transactions) { Add(t.Date,t.BankId,null,t.Kind == TransactionKind.Income ? t.NetIncome : -(t.Amount+t.Fee)); if (t.ToBankId is Guid target) Add(t.Date,target,null,t.Amount); }
        foreach (var e in WishlistEvents) foreach (var c in AllocationChanges(e)) Add(e.Date,c.Bank,e.WishlistId,c.Delta);
        var balances = Banks.ToDictionary(b => b.Id,b => 0m); var totals = Banks.ToDictionary(b => b.Id,b => 0m); var allocations = new Dictionary<(Guid Wish,Guid Bank),decimal>();
        foreach (var (date,changes) in dates)
        {
            foreach (var c in changes)
                if (c.Wish is Guid wish) { var key = (wish,c.Bank); allocations[key] = allocations.GetValueOrDefault(key)+c.Delta; totals[c.Bank]+=c.Delta; }
                else balances[c.Bank]+=c.Delta;
            Require(allocations.Values.All(v => v >= 0),"Wishlist allocation would become negative in the recorded history. Check the activity dates.");
            foreach (var b in Banks) Require(totals[b.Id] == 0 || balances[b.Id] >= totals[b.Id],$"Insufficient available balance in {b.Name} on {Display.Date(date)}. Wishlist funds are protected. Release or move the allocation first.");
        }
    }
}

public static class WishlistService
{
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    private static void Require(bool condition,string message) => FinanceDocument.Require(condition,message);
    private static void Planned(FinanceDocument d,Guid id) => Require(d.Wishlists.Any(w => w.Id == id) && d.WishlistState(id) == WishlistStatus.Planned,"Choose a planned wishlist.");
    private static void Event(FinanceDocument d,Guid id,DateOnly date,WishlistEventKind kind,decimal amount=0,Guid? from=null,Guid? to=null,Guid? tx=null,decimal fee=0,string text="")
    {
        Require(date != default && date <= Today,"Wishlist activity date must be today or earlier.");
        if (kind is not WishlistEventKind.Created and not WishlistEventKind.Edited)
        {
            var latest = d.WishlistEvents.Where(e => e.WishlistId == id && e.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited).Select(e => e.Date).DefaultIfEmpty(DateOnly.MinValue).Max();
            Require(date >= latest,"Choose a date on or after the latest wishlist funds activity.");
        }
        d.WishlistEvents.Add(new(Guid.NewGuid(),id,date,kind,from,to,amount,tx,fee,text));
    }
    private static FinanceDocument Commit(FinanceDocument d) { d.Validate(); return d; }
    public static FinanceDocument Create(FinanceDocument doc,WishlistItem item,DateOnly? plannedDate=null)
    {
        var d=doc.Copy(); d.Wishlists.Add(item); Event(d,item.Id,plannedDate??Today,WishlistEventKind.Created,text:$"Created with target {Display.Money(item.TargetAmount)}. No initial allocation."); return Commit(d);
    }
    public static FinanceDocument Edit(FinanceDocument doc,WishlistItem item,DateOnly? updateDate=null,DateOnly? plannedDate=null)
    {
        Planned(doc,item.Id); var d=doc.Copy(); d.Wishlists[d.Wishlists.FindIndex(w=>w.Id==item.Id)]=item;
        if(plannedDate is DateOnly date)d=EditActivityDate(d,d.WishlistEvents.Single(e=>e.WishlistId==item.Id&&e.Kind==WishlistEventKind.Created).Id,date);
        var at=updateDate??Today;var created=d.WishlistEvents.Single(e=>e.WishlistId==item.Id&&e.Kind==WishlistEventKind.Created);
        Require(at>=created.Date,$"Update Date cannot be before Planned Date ({Display.Date(created.Date)}).");
        Event(d,item.Id,at,WishlistEventKind.Edited,text:$"Wishlist updated. Target {Display.Money(item.TargetAmount)}; allocations retained."); return Commit(d);
    }
    public static FinanceDocument EditActivityDate(FinanceDocument doc,Guid activityId,DateOnly date)
    {
        var index=doc.WishlistEvents.FindIndex(e=>e.Id==activityId);
        Require(index>=0,"Choose an existing wishlist activity.");var activity=doc.WishlistEvents[index];
        Require(Enum.IsDefined(activity.Kind),"Choose a valid wishlist activity.");
        Require(date!=default&&date<=Today,"Activity date must be today or earlier.");
        if(activity.Kind==WishlistEventKind.Edited){var created=doc.WishlistEvents.Single(e=>e.WishlistId==activity.WishlistId&&e.Kind==WishlistEventKind.Created);Require(date>=created.Date,$"Update Date cannot be before Planned Date ({Display.Date(created.Date)}). Correct Planned Date first if needed.");}
        else if(activity.Kind==WishlistEventKind.Created)Require(!doc.WishlistEvents.Any(e=>e.WishlistId==activity.WishlistId&&e.Kind==WishlistEventKind.Edited&&e.Date<date),"Planned Date cannot be after an existing Update Date. Correct the earlier update dates first if needed.");
        else CheckFundsDate(doc,activity,date);
        var d=doc.Copy();d.WishlistEvents[index]=activity with {Date=date};
        if(activity.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred){
            var reversed=activity.Kind==WishlistEventKind.Purchased&&doc.WishlistEvents.Any(e=>e.Kind==WishlistEventKind.PurchaseCorrected&&e.TransactionId==activity.TransactionId);
            var transactionIndex=d.Transactions.FindIndex(t=>t.Id==activity.TransactionId);
            if(reversed)Require(transactionIndex<0,"A reversed purchase must not have an expense in the ledger.");
            else{Require(transactionIndex>=0,"The linked wishlist transaction is missing.");d.Transactions[transactionIndex]=d.Transactions[transactionIndex] with {Date=date};}
        }
        return Commit(d);
    }
    public static FinanceDocument EditTargetDate(FinanceDocument doc,Guid wishlistId,DateOnly? date)
    {
        var index=doc.Wishlists.FindIndex(w=>w.Id==wishlistId);Require(index>=0,"Choose an existing wishlist.");
        Require(date is null||date!=default(DateOnly),"Enter a valid target date or leave it blank.");
        var d=doc.Copy();d.Wishlists[index]=d.Wishlists[index] with {TargetDate=date};return Commit(d);
    }
    public static FinanceDocument Allocate(FinanceDocument doc,Guid id,Guid bank,decimal amount,DateOnly date)
    {
        Planned(doc,id); Require(doc.Banks.Any(b=>b.Id==bank),"Choose an existing bank."); Require(amount>0 && amount<=doc.AvailableBalance(bank,date),"Insufficient available balance. Other wishlist funds cannot be used."); var d=doc.Copy();
        Event(d,id,date,WishlistEventKind.Allocated,amount,to:bank,text:$"Allocated {Display.Money(amount)} from {doc.BankName(bank)}."); return Commit(d);
    }
    public static FinanceDocument EditAllocationDate(FinanceDocument doc,Guid activityId,DateOnly date)
    {
        var index=doc.WishlistEvents.FindIndex(e=>e.Id==activityId);
        Require(index>=0,"Choose an existing allocation activity.");
        var activity=doc.WishlistEvents[index];
        Require(activity.Kind==WishlistEventKind.Allocated,"Choose an Allocate activity.");return EditActivityDate(doc,activityId,date);
    }
    private static void CheckFundsDate(FinanceDocument doc,WishlistEvent activity,DateOnly date)
    {
        var history=doc.WishlistEvents.Where(e=>e.WishlistId==activity.WishlistId && e.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited).ToList();
        var position=history.FindIndex(e=>e.Id==activity.Id);
        if(position>0)Require(date>=history[position-1].Date,$"Choose a date on or after {WishlistPresentation.ActivityName(history[position-1].Kind)} ({Display.Date(history[position-1].Date)}). To move several activities earlier, edit the oldest first.");
        if(position<history.Count-1)Require(date<=history[position+1].Date,$"Choose a date on or before {WishlistPresentation.ActivityName(history[position+1].Kind)} ({Display.Date(history[position+1].Date)}). To move several activities later, edit the newest first.");
    }
    public static FinanceDocument EditPurchaseDate(FinanceDocument doc,Guid activityId,DateOnly date)
    {
        var index=doc.WishlistEvents.FindIndex(e=>e.Id==activityId);
        Require(index>=0,"Choose an existing purchase activity.");var activity=doc.WishlistEvents[index];
        Require(activity.Kind==WishlistEventKind.Purchased,"Choose a Purchased activity.");
        return EditActivityDate(doc,activityId,date);
    }
    public static FinanceDocument Release(FinanceDocument doc,Guid id,Guid bank,decimal amount,DateOnly date)
    {
        Require(doc.WishlistState(id) is WishlistStatus.Planned or WishlistStatus.Purchased,"This wishlist cannot release funds.");
        Require(amount>0 && amount<=doc.WishlistAllocated(id,bank,date),"Amount exceeds this wishlist's allocation in the selected account."); var d=doc.Copy();
        Event(d,id,date,WishlistEventKind.Released,amount,from:bank,text:$"Released {Display.Money(amount)} to {doc.BankName(bank)} available balance."); return Commit(d);
    }
    public static FinanceDocument Move(FinanceDocument doc,Guid id,Guid from,Guid to,decimal amount,decimal fee,DateOnly date,bool transfer)
    {
        Planned(doc,id); Require(doc.Banks.Any(b=>b.Id==from) && doc.Banks.Any(b=>b.Id==to),"Choose existing accounts."); Require(from!=to,"Choose two different accounts."); Require(amount>0 && amount<=doc.WishlistAllocated(id,from,date),$"Insufficient wishlist allocation in {doc.BankName(from)} on {Display.Date(date)}. Allocated: {Display.Money(doc.WishlistAllocated(id,from,date))}. Check the Allocate activity date or allocate funds first.");
        Require(fee>=0 && FinanceDocument.IsMoney(fee),"Enter a valid admin fee.");
        Require(transfer ? fee<=doc.AvailableBalance(from,date) : fee==0 && amount<=doc.AvailableBalance(to,date),transfer ? "The admin fee must be covered by available balance." : "Reassignment requires sufficient available balance in the destination account and no admin fee.");
        var d=doc.Copy(); Guid? tx=null;
        if (transfer) { tx=Guid.NewGuid(); d.Transactions.Add(new(tx.Value,date,TransactionKind.Transfer,from,to,null,amount,fee,null,"Wishlist transfer · "+d.Wishlists.Single(w=>w.Id==id).Name)); }
        Event(d,id,date,transfer?WishlistEventKind.Transferred:WishlistEventKind.Reassigned,amount,from,to,tx,fee,$"{(transfer?"Transferred":"Reassigned")} {Display.Money(amount)}: {doc.BankName(from)} → {doc.BankName(to)}. Admin fee {Display.Money(fee)} from available balance."); return Commit(d);
    }
    public static FinanceDocument Purchase(FinanceDocument doc,Guid id,Guid bank,Guid category,decimal amount,decimal fee,DateOnly date)
    {
        Planned(doc,id); Require(doc.Banks.Any(b=>b.Id==bank),"Choose an existing bank."); Require(amount>0 && amount<=doc.WishlistAllocated(id,bank,date),"The selected account needs enough allocation for this wishlist. Move or allocate funds first; available balance cannot cover the purchase price.");
        Require(fee>=0 && fee<=doc.AvailableBalance(bank,date),"The admin fee must be covered by available balance.");
        var d=doc.Copy(); var tx=Guid.NewGuid(); d.Transactions.Add(new(tx,date,TransactionKind.Expense,bank,null,category,amount,fee,null,"Wishlist purchase · "+d.Wishlists.Single(w=>w.Id==id).Name));
        Event(d,id,date,WishlistEventKind.Purchased,amount,from:bank,tx:tx,fee:fee,text:$"Purchased for {Display.Money(amount)} from {doc.BankName(bank)}. Status remains Purchased until manually closed."); return Commit(d);
    }
    public static FinanceDocument ReleaseBalance(FinanceDocument doc,Guid id,DateOnly date)
    {
        Require(doc.WishlistState(id)==WishlistStatus.Purchased,"Release Balance is available after purchase."); Require(doc.WishlistAllocated(id,null,date)>0,"No allocation remains to release."); var d=doc;
        foreach(var b in doc.Banks) { var amount=doc.WishlistAllocated(id,b.Id,date); if(amount>0)d=Release(d,id,b.Id,amount,date); } return d;
    }
    public static FinanceDocument Cancel(FinanceDocument doc,Guid id,DateOnly date)
    {
        Planned(doc,id); var d=doc;
        foreach(var b in doc.Banks) { var amount=doc.WishlistAllocated(id,b.Id,date); if(amount>0)d=Release(d,id,b.Id,amount,date); }
        d=d.Copy(); Event(d,id,date,WishlistEventKind.Cancelled,text:"Cancelled by user. All allocations released; bank balances unchanged."); return Commit(d);
    }
    public static FinanceDocument Close(FinanceDocument doc,Guid id,DateOnly date)
    {
        Require(doc.WishlistState(id)==WishlistStatus.Purchased && doc.WishlistAllocated(id,null,date)==0,"Purchase the item and use Release Balance before closing.");
        var d=doc.Copy(); Event(d,id,date,WishlistEventKind.Closed,text:"Closed by user."); return Commit(d);
    }
    public static FinanceDocument CorrectPurchase(FinanceDocument doc,Guid id,DateOnly date)
    {
        Require(doc.WishlistState(id) is WishlistStatus.Purchased or WishlistStatus.Closed,"There is no purchase to correct.");
        var purchase=doc.WishlistEvents.Last(e=>e.WishlistId==id && e.Kind==WishlistEventKind.Purchased);
        var d=doc.Copy(); d.Transactions.RemoveAll(t=>t.Id==purchase.TransactionId);
        Event(d,id,date,WishlistEventKind.PurchaseCorrected,purchase.Amount,to:purchase.FromBankId,tx:purchase.TransactionId,fee:purchase.Fee,text:$"Purchase reversed. Restored {Display.Money(purchase.Amount)} to {doc.BankName(purchase.FromBankId!.Value)} allocation. Previously released funds remain available."); return Commit(d);
    }
}

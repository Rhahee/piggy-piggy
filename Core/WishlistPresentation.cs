namespace MyPiggyBank.Core;

public sealed record WishlistDateLimits(DateOnly Earliest,DateOnly Latest);

// Read-only wording and date guidance; the financial validation remains authoritative.
public static class WishlistPresentation
{
    public static string ActivityName(WishlistEventKind kind) => kind switch
    {
        WishlistEventKind.Edited => "Updated",
        WishlistEventKind.PurchaseCorrected => "Purchase Corrected",
        _ => kind.ToString()
    };
    public static string Description(WishlistEvent activity) => activity.Kind==WishlistEventKind.Purchased
        ? activity.Description+"\nAdmin Fee: "+Display.Money(activity.Fee)
        : activity.Description;
    public static WishlistDateLimits DateLimits(FinanceDocument doc,WishlistEvent activity,DateOnly today)
    {
        var earliest=DateOnly.MinValue;var latest=today;
        var events=doc.WishlistEvents.Where(e=>e.WishlistId==activity.WishlistId).ToList();
        if(activity.Kind==WishlistEventKind.Created){foreach(var e in events.Where(e=>e.Kind==WishlistEventKind.Edited))if(e.Date<latest)latest=e.Date;}
        else if(activity.Kind==WishlistEventKind.Edited)earliest=events.Single(e=>e.Kind==WishlistEventKind.Created).Date;
        else{
            var funds=events.Where(e=>e.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited).ToList();var index=funds.FindIndex(e=>e.Id==activity.Id);
            if(index>0)earliest=funds[index-1].Date;
            if(index>=0&&index<funds.Count-1&&funds[index+1].Date<latest)latest=funds[index+1].Date;
            foreach(var bank in doc.Banks.Where(b=>b.Id==activity.FromBankId||b.Id==activity.ToBankId))if(bank.OpeningDate>earliest)earliest=bank.OpeningDate;
        }
        return new(earliest,latest);
    }
}

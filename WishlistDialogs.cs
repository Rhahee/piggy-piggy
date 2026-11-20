using System.IO;
using System.Windows;
using System.Windows.Controls;
using MyPiggyBank.Core;

namespace MyPiggyBank;

internal sealed class WishlistDetailsDialog : FormWindow
{
    public Guid ResultId { get; private set; }
    public WishlistDetailsDialog(Window owner,DocumentSession session,WishlistItem? existing=null) : base(owner,existing is null?"New Wishlist":"Edit Wishlist","Fields marked * are required. Allocate funds whenever you are ready.",existing is null?"Create Wishlist":"Save Changes",560)
    {
        var name=new TextBox{Text=existing?.Name??"",MaxLength=100}; var target=MoneyInput(existing?.TargetAmount??0);
        var date=DateInput(existing?.TargetDate??DateOnly.FromDateTime(DateTime.Today)); if(existing?.TargetDate is null)date.Text="";
        var created=existing is null?null:session.Document.WishlistEvents.Single(e=>e.WishlistId==existing.Id&&e.Kind==WishlistEventKind.Created);
        var plannedDate=DateInput(created?.Date??DateOnly.FromDateTime(DateTime.Today));
        var updateDate=DateInput(existing is null?DateOnly.FromDateTime(DateTime.Today):session.Document.WishlistEvents.Where(e=>e.WishlistId==existing.Id).Max(e=>e.Date));
        var priority=new ComboBox{ItemsSource=Enum.GetValues<WishlistPriority>(),SelectedItem=existing?.Priority??WishlistPriority.Normal};
        var notes=new TextBox{Text=existing?.Notes??"",MaxLength=2000,Height=70,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true};
        var type = new ComboBox { ItemsSource = new[] { "Single Item", "Shared Budget" }, SelectedIndex = existing?.Goals is null ? 0 : 1, IsEnabled = existing?.Goals is null && (existing is null || session.Document.WishlistState(existing.Id) == WishlistStatus.Planned) };
        Field("Wishlist type", type);
        Field("Name *",name);DateField("Planned Date *",plannedDate);if(existing is not null)DateField("Update Date *",updateDate);Field("Target Amount\n(Rp) *",target);DateField("Target Date",date);Field("Priority",priority);Field("Notes",notes);
        var targetRow = (FrameworkElement)target.Parent;
        var conversionNote = Ui.Text(existing is null ? "" : $"First goal: {existing.Name} · {Display.Money(existing.TargetAmount)}", 13, Ui.Muted);
        AddFields(conversionNote);
        void ShowTarget()
        {
            targetRow.Visibility = type.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            conversionNote.Visibility = existing is { Goals: null } && type.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        }
        type.SelectionChanged += (_, _) => ShowTarget();
        ShowTarget();
        var dateNote=Ui.Text("Planned Date is when this wishlist began. Target Date is the purchase goal. For historical records, choose the original dates. Each funding activity has its own date.",12,Ui.Muted);dateNote.Margin=new Thickness(0,12,0,0);AddFields(dateNote);
        OnSubmit(()=>{
            var shared = type.SelectedIndex == 1;
            var value=shared ? 0 : FormValidation.Money(target);FormValidation.Require(shared || value>0,target,"Target amount must be greater than zero, with at most two decimals.");
            var targetDate=FormValidation.OptionalDate(date);
            var item=new WishlistItem(existing?.Id??Guid.NewGuid(),name.Text.Trim(),value,targetDate,(WishlistPriority)priority.SelectedItem,notes.Text.Trim(), shared ? existing?.Goals ?? [] : null);
            var planned=ReadDate(plannedDate);FormValidation.Require(planned!=default&&planned<=DateOnly.FromDateTime(DateTime.Today),plannedDate,"Wishlist activity date must be today or earlier.");
            FormValidation.At(existing is null?plannedDate:updateDate,()=>session.Apply(existing is null?WishlistService.Create(session.Document,item,planned):WishlistService.Edit(session.Document,item,ReadDate(updateDate),planned)));ResultId=item.Id;
        });Loaded+=(_,_)=>name.Focus();
    }
}

internal sealed class WishlistGoalDialog : FormWindow
{
    public WishlistGoalDialog(Window owner, DocumentSession session, WishlistItem budget, WishlistGoal? existing = null)
        : base(owner, existing is null ? "Add Goal" : "Edit Goal", budget.Name, "Save Goal", 480)
    {
        var name = new TextBox { Text = existing?.Name ?? "", MaxLength = 100 };
        var estimate = MoneyInput(existing?.Estimate ?? 0);
        if (existing?.Estimate is null) estimate.Clear();
        Field("Goal name *", name);
        Field("Estimate (Rp)", estimate);
        OnSubmit(() => {
            var price = FormValidation.Money(estimate, true);
            var goal = new WishlistGoal(existing?.Id ?? Guid.NewGuid(), name.Text.Trim(), price == 0 ? null : price);
            var goals = existing is null ? budget.Goals!.Append(goal).ToArray() : budget.Goals!.Select(g => g.Id == existing.Id ? goal : g).ToArray();
            session.Apply(WishlistService.Edit(session.Document, budget with { Goals = goals }));
        });
    }
}

internal sealed class WishlistActivityDateDialog : FormWindow
{
    public WishlistActivityDateDialog(Window owner,DocumentSession session,WishlistEvent activity) : base(owner,activity.Kind switch{WishlistEventKind.Allocated=>"Edit Allocation Date",WishlistEventKind.Purchased=>"Edit Purchase Date",WishlistEventKind.Transferred=>"Edit Transfer Date",WishlistEventKind.Reassigned=>"Edit Reassignment Date",WishlistEventKind.Released=>"Edit Release Date",WishlistEventKind.PurchaseCorrected=>"Edit Correction Date",WishlistEventKind.Cancelled=>"Edit Cancellation Date",WishlistEventKind.Closed=>"Edit Close Date",_=>"Edit Wishlist Date"},"Correct the recorded date. Changes are saved to your file with Save (Ctrl+S).","Save Changes",560)
    {
        var date=DateInput(activity.Date);
        Field("Wishlist",Ui.Text(session.Document.Wishlists.Single(w=>w.Id==activity.WishlistId).Name,13));
        Field("Activity",Ui.Text(WishlistPresentation.ActivityName(activity.Kind),13));
        if(activity.FromBankId is Guid from)Field("From Bank",Ui.Text(session.Document.BankName(from),13));
        if(activity.ToBankId is Guid to)Field("To Bank",Ui.Text(session.Document.BankName(to),13));
        if(activity.Amount>0)Field(activity.Kind==WishlistEventKind.Purchased?"Actual Price":"Amount",Ui.Text(Display.Money(activity.Amount),13));
        if(activity.Fee>0||activity.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred)Field("Admin Fee",Ui.Text(Display.Money(activity.Fee),13));
        DateField(activity.Kind switch{WishlistEventKind.Created=>"Planned Date *",WishlistEventKind.Edited=>"Update Date *",WishlistEventKind.Purchased=>"Purchase Date *",_=>"Date *"},date);
        var limits=WishlistPresentation.DateLimits(session.Document,activity,DateOnly.FromDateTime(DateTime.Today));
        Field("Date range",Ui.Text(limits.Earliest>limits.Latest?"No dates available. Check the surrounding activity dates.":limits.Earliest==DateOnly.MinValue?"On or before "+Display.Date(limits.Latest):Display.Date(limits.Earliest)+" – "+Display.Date(limits.Latest),13));
        var bounds=Ui.Text("Based on activity order and account opening dates. Historical balances are checked when saving.",12,Ui.Muted);bounds.Margin=new Thickness(0,4,0,8);AddFields(bounds);
        var reversed=activity.Kind==WishlistEventKind.Purchased&&session.Document.WishlistEvents.Any(e=>e.Kind==WishlistEventKind.PurchaseCorrected&&e.TransactionId==activity.TransactionId);
        var note=Ui.Text(activity.Kind switch{
            WishlistEventKind.Created or WishlistEventKind.Edited=>"Created uses Planned Date. Updated uses Update Date. This correction does not change funding or bank transaction dates.",
            WishlistEventKind.Transferred=>"The linked bank transfer, both account movements and Admin Fee move to this date together. Amounts, accounts and wishlist status stay unchanged.",
            WishlistEventKind.Purchased when !reversed=>"The linked Expense and Admin Fee move to this date together. Amounts, category, payment account and wishlist status stay unchanged.",
            WishlistEventKind.Purchased=>"This purchase was reversed. Only its historical activity date changes; the deleted Expense and fee will not be recreated.",
            WishlistEventKind.PurchaseCorrected=>"Changes when the purchase allocation was restored. The reversed Expense and fee stay removed. The wishlist's current status stays unchanged.",
            _=>"Only this activity date changes. No bank transaction is created, and current amounts, allocations and wishlist status stay unchanged."
        },12,Ui.Muted);note.Margin=new Thickness(0,12,0,0);AddFields(note);
        if(activity.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited){var order=Ui.Text("Dates must fit the surrounding funds activities and historical balances. To move several activities earlier, edit the oldest first; to move them later, edit the newest first.",12,Ui.Muted);order.Margin=new Thickness(0,8,0,0);AddFields(order);}
        OnSubmit(()=>FormValidation.At(date,()=>session.Apply(WishlistService.EditActivityDate(session.Document,activity.Id,ReadDate(date)))));
    }
}

internal sealed class WishlistTargetDateDialog : FormWindow
{
    public WishlistTargetDateDialog(Window owner,DocumentSession session,WishlistItem item) : base(owner,"Edit Target Date","Change the purchase goal date in any wishlist status. Leave blank to remove the target date.","Save Changes",560)
    {
        var date=DateInput(item.TargetDate??DateOnly.FromDateTime(DateTime.Today));if(item.TargetDate is null)date.Text="";
        Field("Wishlist",Ui.Text(item.Name,13));DateField("Target Date",date);
        AddFields(Ui.Text("This changes only the target date. Funding, purchase history and wishlist status stay unchanged. Save your file with Ctrl+S.",12,Ui.Muted));
        OnSubmit(()=>FormValidation.At(date,()=>session.Apply(WishlistService.EditTargetDate(session.Document,item.Id,FormValidation.OptionalDate(date)))));
    }
}

internal sealed class WishlistFundsDialog : FormWindow
{
    private sealed record FundingChoice(Guid? Id, string Name);

    public WishlistFundsDialog(Window owner,DocumentSession session,WishlistItem wish,string action) : base(owner,action switch{"allocate"=>"Allocate Funds","release"=>"Release Funds","move"=>"Move Funds","assign"=>"Reallocate Funds",_=>wish.Goals is null ? "Mark as Purchased" : "Purchase"},action=="release"?"Returns allocation to available balance. No expense is recorded.":action=="assign"?"Choose where to reallocate funds within this bank account.":"Admin fees use available balance.",action=="purchase"?"Record Purchase":action=="release"?"Release Funds":"Confirm",600)
    {
        var doc=session.Document; var banks=doc.Banks.OrderBy(b=>b.Name).ToList();
        var date=DateInput(DateOnly.FromDateTime(DateTime.Today)); var bank=Combo(banks); bank.SelectedItem=action is "release" or "move" or "purchase"?banks.OrderByDescending(b=>doc.WishlistAllocated(wish.Id,b.Id)).FirstOrDefault():banks.FirstOrDefault();
        var target=Combo(banks);target.SelectedItem=banks.FirstOrDefault(b=>b.Id!=(bank.SelectedItem as Bank)?.Id);
        var method=new ComboBox{ItemsSource=new[]{"Transfer Between Accounts","Reassign Allocation"},SelectedIndex=0};
        var amount=MoneyInput(action=="purchase"?Math.Min(wish.TargetAmount,doc.WishlistAllocated(wish.Id)):0);var fee=MoneyInput();
        var categories=new ComboBox{DisplayMemberPath="Name"};
        void Categories(Guid? selected=null){var items=session.Document.Categories.Where(c=>c.Kind==CategoryKind.Expense).OrderBy(c=>c.Name).ToList();categories.ItemsSource=items;categories.SelectedItem=items.FirstOrDefault(c=>c.Id==selected);}
        Categories();
        var summary=Ui.Text("",13,Ui.Muted);summary.Margin=new Thickness(0,16,0,0);
        DateField("Date *",date);
        if(action=="move")Field("Method *",method);
        Field(action=="move"?"From Bank *":action=="purchase"?"Pay from *":"Bank *",bank);
        if(action=="move")Field("To Bank *",target);
        var goal = new ComboBox { ItemsSource = wish.ActiveGoals.ToArray(), DisplayMemberPath = "Name", SelectedIndex = -1 };
        var choices = new[] { new FundingChoice(null, "Unassigned balance") }
            .Concat(wish.ActiveGoals.Select(g => new FundingChoice(g.Id, g.Name))).ToArray();
        var source = new ComboBox { ItemsSource = choices, DisplayMemberPath = "Name", SelectedIndex = 0 };
        var destination = new ComboBox { ItemsSource = doc.Wishlists.Where(w => doc.WishlistState(w.Id) == WishlistStatus.Planned && w.Goals is not null).ToArray(), DisplayMemberPath = "Name", SelectedValuePath = "Id", SelectedValue = wish.Id };
        var destinationGoal = new ComboBox { DisplayMemberPath = "Name" };
        void RefreshDestination()
        {
            var selected = destination.SelectedItem as WishlistItem;
            destinationGoal.ItemsSource = new[] { new FundingChoice(null, "Unassigned balance") }
                .Concat(selected?.ActiveGoals.Select(g => new FundingChoice(g.Id, g.Name)) ?? []).ToArray();
            destinationGoal.SelectedIndex = 0;
        }
        if (wish.Goals is not null && action is ("release" or "move" or "assign")) Field("From allocation *", source);
        if (action == "assign")
        {
            Field("To wishlist *", destination);
            Field("To allocation *", destinationGoal);
            destination.SelectionChanged += (_, _) => RefreshDestination();
            RefreshDestination();
        }
        Guid? FundingGoal() => action == "purchase" ? (goal.SelectedItem as WishlistGoal)?.Id : (source.SelectedItem as FundingChoice)?.Id;
        if(action=="purchase"){
            if (wish.Goals is not null) Field("Goal *", goal);
            var row=new DockPanel();var add=Ui.Button("New",()=>{var dialog=new CategoryDialog(this,session.Document,forced:CategoryKind.Expense);if(dialog.ShowDialog()==true){var copy=session.Document.Copy();copy.Categories.Add(dialog.Result!);session.Apply(copy);Categories(dialog.Result!.Id);}});
            add.Margin=new Thickness(4,0,0,0);add.Foreground=Ui.Accent;add.BorderThickness=new Thickness(0);DockPanel.SetDock(add,Dock.Right);row.Children.Add(add);row.Children.Add(categories);Field("Category *",row);
        }
        Field(action=="purchase"?"Actual Price (Rp) *":"Amount (Rp) *",amount);
        if(action is "move" or "purchase")Field("Admin Fee (Rp)",fee);
        AddFields(summary);
        void Refresh(){
            fee.IsEnabled=action!="move"||method.SelectedIndex==0;
            try{
                if(bank.SelectedItem is not Bank b)return;var at=ReadDate(date);var allocated=session.Document.WishlistBucket(wish.Id,FundingGoal(),b.Id,at);var available=session.Document.AvailableBalance(b.Id,at);
                summary.Text=$"As of {Display.Date(at)}\n{b.Name} available: {Display.Money(available)}\nSelected allocation: {Display.Money(allocated)}";
                if(action=="move"&&target.SelectedItem is Bank to)summary.Text+=$"\n{to.Name} available: {Display.Money(session.Document.AvailableBalance(to.Id,at))}\n"+(method.SelectedIndex==0?"A bank transfer is recorded; admin fee uses source available balance.":"Only the allocation changes. No transfer or admin fee is recorded.");
                if(action=="purchase"){var price=FormValidation.Money(amount,true);summary.Text+=price>allocated?$"\nAllocation shortfall: {Display.Money(price-allocated)}. Move or allocate funds first.":$"\n{(wish.Goals is null ? "Remaining allocation" : "Remainder returned to group balance")} in this account: {Display.Money(allocated-price)}";}
            }catch(Exception e)when(e is InvalidDataException or FormatException){summary.Text=e.Message;}
        }
        source.SelectionChanged += (_, _) => Refresh(); goal.SelectionChanged += (_, _) => Refresh();
        bank.SelectionChanged+=(_,_)=>Refresh();target.SelectionChanged+=(_,_)=>Refresh();method.SelectionChanged+=(_,_)=>Refresh();date.TextChanged+=(_,_)=>Refresh();amount.TextChanged+=(_,_)=>Refresh();Refresh();
        OnSubmit(()=>{
            FormValidation.Require(bank.SelectedItem is Bank,bank,"Choose a bank.");var b=(Bank)bank.SelectedItem;var at=ReadDate(date);var value=FormValidation.Money(amount);var admin=fee.IsEnabled?FormValidation.Money(fee,true):0;
            FormValidation.Require(value>0,amount,"Amount must be greater than zero, with at most two decimals.");
            FormValidation.Require(admin>=0,fee,"Admin fee must be zero or greater, with at most two decimals.");
            FormValidation.Require(at!=default&&at<=DateOnly.FromDateTime(DateTime.Today),date,"Wishlist activity date must be today or earlier.");
            var latest=session.Document.WishlistEvents.Where(e=>e.WishlistId==wish.Id&&e.Kind is not WishlistEventKind.Created and not WishlistEventKind.Edited).Select(e=>e.Date).DefaultIfEmpty(DateOnly.MinValue).Max();
            FormValidation.Require(at>=latest,date,"Choose a date on or after the latest wishlist funds activity.");
            if(action=="move")FormValidation.Require(target.SelectedItem is Bank to&&to.Id!=b.Id,target,"Choose two different banks to move funds.");
            if (action == "purchase" && wish.Goals is not null) FormValidation.Require(goal.SelectedItem is WishlistGoal, goal, "Choose a goal.");
            var updated=FormValidation.At(amount,()=>action switch{
                "allocate"=>WishlistService.Allocate(session.Document,wish.Id,b.Id,value,at),
                "release"=>WishlistService.Release(session.Document,wish.Id,b.Id,value,at,FundingGoal()),
                "assign"=>WishlistService.AssignFunds(session.Document,wish.Id,FundingGoal(),((WishlistItem)destination.SelectedItem).Id,((FundingChoice)destinationGoal.SelectedItem).Id,b.Id,value,at),
                "move"=>WishlistService.Move(session.Document,wish.Id,b.Id,(target.SelectedItem as Bank)?.Id??Guid.Empty,value,method.SelectedIndex==0?admin:0,at,method.SelectedIndex==0,FundingGoal()),
                _=>WishlistService.Purchase(session.Document,wish.Id,b.Id,(categories.SelectedItem as Category)?.Id??Guid.Empty,value,admin,at,(goal.SelectedItem as WishlistGoal)?.Id)
            });session.Apply(updated);
        });
    }
}

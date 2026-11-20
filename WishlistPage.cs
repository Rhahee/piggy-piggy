using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow
{
    private Guid? selectedWishlist;
    private WishlistItem? ActiveWishlist => session.Document.Wishlists.FirstOrDefault(w=>w.Id==selectedWishlist);
    private void ShowWishlist(){page="Wishlist";backstage=false;Render();}
    private UIElement WishlistSummary(IEnumerable<Bank> banks)
    {
        var list=banks.ToList();var today=DateOnly.FromDateTime(DateTime.Today);var grid=new Grid();
        var values=new[]{("Balance",list.Sum(b=>session.Document.Balance(b.Id,today))),("Wishlist Allocation",list.Sum(b=>session.Document.WishlistReserved(b.Id,today))),("Available Balance",list.Sum(b=>session.Document.AvailableBalance(b.Id,today)))};
        for(var i=0;i<values.Length;i++){
            grid.ColumnDefinitions.Add(new ColumnDefinition());var panel=Ui.Stack();panel.Children.Add(Ui.Text(values[i].Item1,12,Ui.Muted));var amount=Ui.Text(Display.Money(values[i].Item2),18,i==1?Ui.Accent:Ui.Ink);amount.Margin=new Thickness(0,4,0,0);panel.Children.Add(amount);
            var border=new Border{Child=panel,Padding=new Thickness(12),BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,i<2?1:0,0)};Grid.SetColumn(border,i);grid.Children.Add(border);
        }
        return new Border{Child=grid,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)};
    }
    private static decimal WishlistTarget(WishlistItem item) =>
        item.Goals is null ? item.TargetAmount : item.ActiveGoals.Sum(goal => goal.Estimate ?? 0);

    private static ProgressBar WishlistProgress(decimal allocated, decimal target, string label, Thickness margin)
    {
        var bar = new ProgressBar { Maximum = 100, Value = (double)(Math.Min(allocated, target) / target * 100),
            Height = 4, Foreground = Ui.Accent, Background = Ui.Line, BorderThickness = new Thickness(0), Margin = margin };
        System.Windows.Automation.AutomationProperties.SetName(bar, label);
        return bar;
    }

    private UIElement WishlistPage()
    {
        var doc=session.Document;var outer=new DockPanel{Background=Brushes.White};
        var items=doc.Wishlists.Where(w=>selectedBank is null||doc.WishlistAllocated(w.Id,selectedBank)>0).ToList();
        if(!items.Any(w=>w.Id==selectedWishlist))selectedWishlist=items.FirstOrDefault()?.Id;
        var header=new DockPanel{Margin=new Thickness(12)};var add=Ui.Button("New Wishlist",()=>WishlistAction("new"),true);DockPanel.SetDock(add,Dock.Right);header.Children.Add(add);header.Children.Add(Ui.Text("Wishlist",24,bold:true));DockPanel.SetDock(header,Dock.Top);outer.Children.Add(header);
        var summary=WishlistSummary(doc.Banks.Where(b=>selectedBank is null||b.Id==selectedBank));DockPanel.SetDock(summary,Dock.Top);outer.Children.Add(summary);
        if(items.Count==0){var empty=Ui.Stack();empty.Margin=new Thickness(24);empty.Children.Add(Ui.Text(doc.Wishlists.Count==0?"Plan your next purchase":"No allocations in this account",20,bold:true));var note=Ui.Text(doc.Wishlists.Count==0?"Create a wishlist now. Allocate funds from your accounts whenever you are ready.":"Choose All accounts to see wishlists without allocations or funded by other banks.",14,Ui.Muted);note.Margin=new Thickness(0,12,0,0);empty.Children.Add(note);outer.Children.Add(empty);return outer;}
        var selectionChanged=renderedWishlist!=selectedWishlist;renderedWishlist=selectedWishlist;
        var split=new Grid{Margin=new Thickness(12)};wishlistSplit=split;split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(wishlistLeftWidth),MinWidth=180});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(4)});split.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star),MinWidth=380});
        Ui.ConstrainSplitList(outer,split);
        var list=Ui.Stack();FrameworkElement? selectedButton=null;
        foreach(var w in items){
            var body=Ui.Stack();var name=Ui.Text(w.Name,14,bold:true);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=w.Name;body.Children.Add(name);
            var status=Ui.Text(doc.WishlistState(w.Id).ToString(),11,Ui.Muted);status.Margin=new Thickness(0,4,0,4);body.Children.Add(status);
            var allocated = doc.WishlistAllocated(w.Id);
            var target = WishlistTarget(w);
            var planned = doc.WishlistState(w.Id) == WishlistStatus.Planned;
            body.Children.Add(Ui.Text(Display.Money(allocated), 14, Ui.Accent));
            body.Children.Add(Ui.Text(planned && target > 0 ? "of " + Display.Money(target) : w.Goals is not null ? "Shared Budget" : "Remaining allocation", 12, Ui.Muted));
            if (planned && target > 0)
                body.Children.Add(WishlistProgress(allocated, target, w.Name + " funding progress", new Thickness(0,10,0,0)));
            var button=Ui.Button("",()=>{selectedWishlist=w.Id;Render();});button.Content=body;button.HorizontalContentAlignment=HorizontalAlignment.Stretch;button.Padding=new Thickness(12);button.Margin=new Thickness(0);button.Background=w.Id==selectedWishlist?Ui.Pale:Brushes.White;button.BorderThickness=new Thickness(0);
            if(w.Id==selectedWishlist)selectedButton=button;
            list.Children.Add(new Border{Child=button,BorderBrush=w.Id==selectedWishlist?Ui.Accent:Ui.Line,BorderThickness=new Thickness(w.Id==selectedWishlist?3:0,0,0,1)});
        }
        var listScroll=new ScrollViewer{Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};wishlistListScroll=listScroll;split.Children.Add(listScroll);
        var divider=new GridSplitter{Width=4,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Ui.Line};Grid.SetColumn(divider,1);split.Children.Add(divider);
        wishlistActivityRows=[];var detail=new ScrollViewer{Content=WishlistDetail(ActiveWishlist!),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(16,0,0,0)};wishlistDetailScroll=detail;RestoreWishlistViewport(listScroll,detail,ActiveWishlist!.Id,selectionChanged,selectedButton);Grid.SetColumn(detail,2);split.Children.Add(detail);outer.Children.Add(split);return outer;
    }
    private UIElement WishlistDetail(WishlistItem w)
    {
        var doc=session.Document;var status=doc.WishlistState(w.Id);var allocated=doc.WishlistAllocated(w.Id);var panel=Ui.Stack();
        var heading=new DockPanel();var badge=Ui.Text(status.ToString(),12,Ui.Muted);DockPanel.SetDock(badge,Dock.Right);heading.Children.Add(badge);heading.Children.Add(Ui.Text(w.Name,22,bold:true));panel.Children.Add(heading);
        var planned=doc.WishlistEvents.Single(e=>e.WishlistId==w.Id&&e.Kind==WishlistEventKind.Created).Date;
        var metaRow=new DockPanel{Margin=new Thickness(0,6,0,16)};var targetDate=Ui.Button("Edit Target Date",()=>{new WishlistTargetDateDialog(this,session,w).ShowDialog();Render();});targetDate.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(targetDate,Dock.Right);metaRow.Children.Add(targetDate);
        metaRow.Children.Add(Ui.Text("Planned: "+Display.Date(planned)+"    Priority: "+w.Priority+(w.TargetDate is DateOnly date?"    Target: "+Display.Date(date):""),12,Ui.Muted));panel.Children.Add(metaRow);
        var metrics=new Grid();metrics.ColumnDefinitions.Add(new ColumnDefinition());metrics.ColumnDefinitions.Add(new ColumnDefinition());
        var targetAmount = WishlistTarget(w);
        var target = Ui.Stack();
        target.Children.Add(Ui.Text(w.Goals is null ? "Target Amount" : "Total Goal Estimates", 12, Ui.Muted));
        target.Children.Add(Ui.Text(targetAmount > 0 ? Display.Money(targetAmount) : "No estimates yet", 20));
        metrics.Children.Add(target);
        var funding=Ui.Stack();funding.Children.Add(Ui.Text(status==WishlistStatus.Planned?"Allocated Funds":"Remaining Allocation",12,Ui.Muted));funding.Children.Add(Ui.Text(Display.Money(allocated),20,Ui.Accent));Grid.SetColumn(funding,1);metrics.Children.Add(funding);panel.Children.Add(metrics);
        if (status == WishlistStatus.Planned && targetAmount > 0)
        {
            panel.Children.Add(WishlistProgress(allocated, targetAmount, w.Name + " funding progress", new Thickness(0,12,0,4)));
            var difference = targetAmount - allocated;
            panel.Children.Add(Ui.Text($"{Math.Min(allocated, targetAmount) / targetAmount:P0} funded    " +
                (difference < 0 ? "Surplus " : "Remaining ") + Display.Money(Math.Abs(difference)), 12, Ui.Muted));
        }
        else if(status is WishlistStatus.Purchased or WishlistStatus.Closed){var purchase=doc.WishlistEvents.Last(e=>e.WishlistId==w.Id&&e.Kind==WishlistEventKind.Purchased);var text=Ui.Text($"Actual Purchase: {Display.Money(purchase.Amount)} · {doc.BankName(purchase.FromBankId!.Value)}\nAdmin Fee: {Display.Money(purchase.Fee)}",14);text.Margin=new Thickness(0,12,0,0);panel.Children.Add(text);}
        if (w.Goals is not null)
            panel.Children.Add(WishlistGoals(w, status == WishlistStatus.Planned));
        var actions=new WrapPanel{Margin=new Thickness(0,16,0,16)};
        void Add(string label,string action,bool enabled=true,bool primary=false){var b=Ui.Button(label,()=>WishlistAction(action),primary);b.IsEnabled=enabled;b.Margin=new Thickness(0,0,4,4);actions.Children.Add(b);}
        if(status==WishlistStatus.Planned){Add("Allocate Funds","allocate",true,true);Add("Move Funds","move",allocated>0);Add("Release Funds","release",allocated>0);Add(w.Goals is null ? "Mark as Purchased" : "Purchase","purchase",w.Goals is null || w.ActiveGoals.Any());}
        if(status==WishlistStatus.Purchased){Add("Release Balance","balance",allocated>0,true);Add("Close Wishlist","close",allocated==0);}
        if (w.Goals is not null && status == WishlistStatus.Planned) Add("Reallocate Funds", "assign", allocated > 0);
        panel.Children.Add(actions);
        if(status==WishlistStatus.Purchased)panel.Children.Add(Ui.Text(allocated>0?"Release remaining allocation before closing. Status stays Purchased until you close it.":"No remaining allocation. Close the wishlist when you are ready.",12,Ui.Muted));
        var banksTitle=Ui.Text("Allocated by Account",16,bold:true);banksTitle.Margin=new Thickness(0,12,0,8);panel.Children.Add(banksTitle);
        foreach(var b in doc.Banks.OrderBy(b=>b.Name)){
            var row=new DockPanel{Margin=new Thickness(0,8,0,8)};var amount=Ui.Text(Display.Money(doc.WishlistAllocated(w.Id,b.Id)),14);DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);var info=Ui.Stack();info.Children.Add(Ui.Text(b.Name,14));info.Children.Add(Ui.Text("Available "+Display.Money(doc.AvailableBalance(b.Id)),12,Ui.Muted));row.Children.Add(info);panel.Children.Add(new Border{Child=row,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)});
        }
        var historyTitle=Ui.Text("Activity",16,bold:true);historyTitle.Margin=new Thickness(0,20,0,8);panel.Children.Add(historyTitle);
        foreach(var e in doc.WishlistEvents.Select((activity,index)=>(activity,index)).Where(x=>x.activity.WishlistId==w.Id).OrderByDescending(x=>x.activity.Date).ThenByDescending(x=>x.index).Select(x=>x.activity)){
            var row=new Grid{Margin=new Thickness(0,8,0,8)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(88)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(Ui.Text(Display.Date(e.Date),12,Ui.Muted));var text=Ui.Text((e.Kind == WishlistEventKind.PurchaseCorrected && e.GoalId is Guid goalId ? w.Goals!.Single(g => g.Id == goalId).Name + " · " : "") + WishlistPresentation.Description(e),12);Grid.SetColumn(text,1);row.Children.Add(text);
            var edit=Ui.Button("Edit Date",()=>EditWishlistActivityDate(e));edit.Margin=new Thickness(8,0,0,0);edit.ToolTip="Edit date: "+WishlistPresentation.ActivityName(e.Kind);Grid.SetColumn(edit,2);if (!e.InternalFunding) row.Children.Add(edit);
            if (w.Goals is not null && status == WishlistStatus.Planned && e.Kind == WishlistEventKind.Purchased && doc.Transactions.Any(t => t.Id == e.TransactionId))
            {
                var buttons = Ui.Stack(); row.Children.Remove(edit); buttons.Children.Add(edit);
                buttons.Children.Add(Ui.Button("Correct Purchase", () => WishlistAction("correct", e)));
                Grid.SetColumn(buttons, 2); row.Children.Add(buttons);
            }
            var activityRow=new Border{Child=row,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)};wishlistActivityRows[e.Id]=activityRow;panel.Children.Add(activityRow);
        }
        if(status is WishlistStatus.Purchased or WishlistStatus.Closed){var correct=Ui.Button("Correct Purchase",()=>WishlistAction("correct"));correct.HorizontalAlignment=HorizontalAlignment.Left;correct.Margin=new Thickness(0,12,0,0);panel.Children.Add(correct);}
        if(!string.IsNullOrWhiteSpace(w.Notes)){var title=Ui.Text("Notes",16,bold:true);title.Margin=new Thickness(0,16,0,8);panel.Children.Add(title);panel.Children.Add(Ui.Text(w.Notes,14,Ui.Muted));}
        return panel;
    }
    private UIElement WishlistGoals(WishlistItem budget, bool editable)
    {
        var doc = session.Document;
        var section = Ui.Stack();
        section.Margin = new Thickness(0, 16, 0, 0);
        var title = Ui.Text("Goals", 16, bold: true);
        title.Margin = new Thickness(0, 0, 0, 8);
        section.Children.Add(title);

        var table = new Grid { MinWidth = editable ? 605 : 465 };
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MinWidth = 120 });
        for (var i = 0; i < 3; i++)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 115 });
        if (editable)
            table.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 140 });

        void AddRow(string name, string allocated, string estimate, string remaining, Brush background, StackPanel? actions = null, bool header = false)
        {
            var row = table.RowDefinitions.Count;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var fill = new Border { Background = background, BorderBrush = Ui.Line, BorderThickness = new Thickness(0, 0, 0, 1) };
            Grid.SetRow(fill, row);
            Grid.SetColumnSpan(fill, table.ColumnDefinitions.Count);
            table.Children.Add(fill);

            var values = new[] { name, allocated, estimate, remaining };
            for (var column = 0; column < values.Length; column++)
            {
                var cell = Ui.Text(values[column], 13, bold: header || row == 1);
                cell.Margin = new Thickness(10, 8, 10, 8);
                cell.TextAlignment = column == 0 ? TextAlignment.Left : TextAlignment.Right;
                Grid.SetRow(cell, row);
                Grid.SetColumn(cell, column);
                table.Children.Add(cell);
            }
            if (actions is not null)
            {
                Grid.SetRow(actions, row);
                Grid.SetColumn(actions, 4);
                table.Children.Add(actions);
            }
        }

        void EditGoal(WishlistGoal? goal)
        {
            if (new WishlistGoalDialog(this, session, budget, goal).ShowDialog() == true) Render();
        }

        AddRow("Goal", "Allocated", "Estimate", "Remaining", Ui.Brush("#F3F3F6"), header: true);
        AddRow("Unassigned balance", Display.Money(doc.WishlistBucket(budget.Id, null)), "—", "—", Ui.Pale);
        foreach (var goal in budget.ActiveGoals)
        {
            StackPanel? actions = null;
            if (editable)
            {
                actions = Ui.Stack(Orientation.Horizontal);
                actions.VerticalAlignment = VerticalAlignment.Center;
                actions.HorizontalAlignment = HorizontalAlignment.Right;
                var edit = Ui.Button("Edit", () => EditGoal(goal));
                var delete = Ui.Button("Delete", () => DeleteWishlistGoal(budget, goal));
                System.Windows.Automation.AutomationProperties.SetName(edit, "Edit " + goal.Name);
                System.Windows.Automation.AutomationProperties.SetName(delete, "Delete " + goal.Name);
                actions.Children.Add(edit);
                actions.Children.Add(delete);
            }
            var balance = doc.WishlistBucket(budget.Id, goal.Id);
            AddRow(goal.Name, Display.Money(balance), goal.Estimate is decimal estimate ? Display.Money(estimate) : "—",
                goal.Estimate is decimal target ? Display.Money(Math.Max(0, target - balance)) : "—", Brushes.White, actions);
        }
        section.Children.Add(new ScrollViewer { Content = table, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        if (editable)
        {
            var add = Ui.Button("Add Goal", () => EditGoal(null));
            add.HorizontalAlignment = HorizontalAlignment.Left;
            section.Children.Add(add);
        }
        return section;
    }

    private void DeleteWishlistGoal(WishlistItem budget, WishlistGoal goal)
    {
        if (!ConfirmDialog.Confirm(this, "Delete Goal?", $"Remove {goal.Name} from {budget.Name}? Any allocated funds will return to the group's unassigned balance. Purchase history is retained.", "Delete Goal")) return;
        try
        {
            session.Apply(WishlistService.DeleteGoal(session.Document, budget.Id, goal.Id, DateOnly.FromDateTime(DateTime.Today)));
            Render();
        }
        catch (InvalidDataException error) { ConfirmDialog.Alert(this, "Wishlist", error.Message); }
    }

    private void WishlistAction(string action, WishlistEvent? purchase = null)
    {
        try{
            if(action=="new"){
                var dialog=new WishlistDetailsDialog(this,session);if(dialog.ShowDialog()==true){selectedWishlist=dialog.ResultId;selectedBank=null;page="Wishlist";}Render();return;
            }
            var w=ActiveWishlist;if(w is null)return;
            if(action=="edit"){new WishlistDetailsDialog(this,session,w).ShowDialog();Render();return;}
            if(action is "allocate" or "release" or "move" or "purchase" or "assign"){
                FinanceDocument.Require(session.Document.Banks.Count>=(action=="move"?2:1),action=="move"?"Add two bank accounts before moving funds.":"Add a bank account first.");new WishlistFundsDialog(this,session,w,action).ShowDialog();Render();return;
            }
            var detail=string.Join("\n",session.Document.Banks.Where(b=>session.Document.WishlistAllocated(w.Id,b.Id)>0).Select(b=>b.Name+": "+Display.Money(session.Document.WishlistAllocated(w.Id,b.Id))));
            var (title,message,button)=action switch{
                "balance"=>("Release Balance?","Release the remaining allocations to available balance in the same accounts?\n\n"+detail,"Release Balance"),
                "cancel"=>("Cancel Wishlist?","Cancel this wishlist and release all its allocations? History is retained.\n\n"+detail,"Cancel Wishlist"),
                "close"=>("Close Wishlist?","Close this purchased wishlist? Purchase and allocation history are retained.","Close Wishlist"),
                "correct" when w.Goals is not null && purchase is not null => ("Correct Purchase?", $"Reverse this expense and restore its allocation?\n\n{Display.Date(purchase.Date)} · {purchase.Description}", "Reverse Purchase"),
                _=>("Correct Purchase?","Reverse the linked expense and reopen as Planned. The last purchase amount returns to this wishlist's allocation. Previously released funds stay available. This corrects a recording error; it is not a store refund.","Reverse and Reopen")
            };
            if(!ConfirmDialog.Confirm(this,title,message,button))return;var today=DateOnly.FromDateTime(DateTime.Today);
            session.Apply(action switch{"balance"=>WishlistService.ReleaseBalance(session.Document,w.Id,today),"cancel"=>WishlistService.Cancel(session.Document,w.Id,today),"close"=>WishlistService.Close(session.Document,w.Id,today),_=>WishlistService.CorrectPurchase(session.Document,w.Id,today,purchase?.Id)});Render();
        }catch(InvalidDataException e){ConfirmDialog.Alert(this,"Wishlist",e.Message);Render();}
    }
    private bool RedirectWishlistTransaction(Transaction transaction,bool correct=false)
    {
        var activity=session.Document.WishlistEvents.FirstOrDefault(e=>e.TransactionId==transaction.Id&&e.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred);if(activity is null)return false;
        selectedWishlist=activity.WishlistId;selectedBank=null;ShowWishlist();
        if(correct&&activity.Kind==WishlistEventKind.Purchased)WishlistAction("correct",activity);
        else ConfirmDialog.Alert(this,"Wishlist-linked transaction",activity.Kind==WishlistEventKind.Purchased?"To correct the purchase date, use Edit Date beside Purchased in Wishlist Activity. For other purchase corrections, use Correct Purchase to reverse and reopen the item.":"To correct this transfer's date, use Edit Date beside Transferred in Wishlist Activity. Other transfer fields cannot be edited or deleted independently. Use Move Funds to record an actual return transfer.");return true;
    }
}

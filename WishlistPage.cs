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
        var list=Ui.Stack();FrameworkElement? selectedButton=null;
        foreach(var w in items){
            var body=Ui.Stack();var name=Ui.Text(w.Name,14,bold:true);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=w.Name;body.Children.Add(name);
            var status=Ui.Text(doc.WishlistState(w.Id).ToString(),11,Ui.Muted);status.Margin=new Thickness(0,4,0,4);body.Children.Add(status);
            body.Children.Add(Ui.Text(Display.Money(doc.WishlistAllocated(w.Id)),14,Ui.Accent));body.Children.Add(Ui.Text(doc.WishlistState(w.Id)==WishlistStatus.Planned?"of "+Display.Money(w.TargetAmount):"Remaining allocation",12,Ui.Muted));
            if(doc.WishlistState(w.Id)==WishlistStatus.Planned){var progress=new ProgressBar{Minimum=0,Maximum=100,Value=(double)Math.Min(100,doc.WishlistAllocated(w.Id)/w.TargetAmount*100),Height=4,BorderThickness=new Thickness(0),Foreground=Ui.Accent,Background=Ui.Line,Margin=new Thickness(0,10,0,0)};body.Children.Add(progress);}
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
        var target=Ui.Stack();target.Children.Add(Ui.Text("Target Amount",12,Ui.Muted));target.Children.Add(Ui.Text(Display.Money(w.TargetAmount),20));metrics.Children.Add(target);
        var funding=Ui.Stack();funding.Children.Add(Ui.Text(status==WishlistStatus.Planned?"Allocated Funds":"Remaining Allocation",12,Ui.Muted));funding.Children.Add(Ui.Text(Display.Money(allocated),20,Ui.Accent));Grid.SetColumn(funding,1);metrics.Children.Add(funding);panel.Children.Add(metrics);
        if(status==WishlistStatus.Planned){panel.Children.Add(new ProgressBar{Minimum=0,Maximum=100,Value=(double)Math.Min(100,allocated/w.TargetAmount*100),Height=4,Foreground=Ui.Accent,Background=Ui.Line,BorderThickness=new Thickness(0),Margin=new Thickness(0,12,0,4)});panel.Children.Add(Ui.Text($"{allocated/w.TargetAmount:P0} funded    "+(allocated>w.TargetAmount?"Surplus "+Display.Money(allocated-w.TargetAmount):"Remaining "+Display.Money(w.TargetAmount-allocated)),12,Ui.Muted));}
        else if(status is WishlistStatus.Purchased or WishlistStatus.Closed){var purchase=doc.WishlistEvents.Last(e=>e.WishlistId==w.Id&&e.Kind==WishlistEventKind.Purchased);var text=Ui.Text($"Actual Purchase: {Display.Money(purchase.Amount)} · {doc.BankName(purchase.FromBankId!.Value)}\nAdmin Fee: {Display.Money(purchase.Fee)}",14);text.Margin=new Thickness(0,12,0,0);panel.Children.Add(text);}
        var actions=new WrapPanel{Margin=new Thickness(0,16,0,16)};
        void Add(string label,string action,bool enabled=true,bool primary=false){var b=Ui.Button(label,()=>WishlistAction(action),primary);b.IsEnabled=enabled;b.Margin=new Thickness(0,0,4,4);actions.Children.Add(b);}
        if(status==WishlistStatus.Planned){Add("Allocate Funds","allocate",true,true);Add("Move Funds","move",allocated>0);Add("Release Funds","release",allocated>0);Add("Mark as Purchased","purchase");}
        if(status==WishlistStatus.Purchased){Add("Release Balance","balance",allocated>0,true);Add("Close Wishlist","close",allocated==0);}
        panel.Children.Add(actions);
        if(status==WishlistStatus.Purchased)panel.Children.Add(Ui.Text(allocated>0?"Release remaining allocation before closing. Status stays Purchased until you close it.":"No remaining allocation. Close the wishlist when you are ready.",12,Ui.Muted));
        var banksTitle=Ui.Text("Allocated by Account",16,bold:true);banksTitle.Margin=new Thickness(0,12,0,8);panel.Children.Add(banksTitle);
        foreach(var b in doc.Banks.OrderBy(b=>b.Name)){
            var row=new DockPanel{Margin=new Thickness(0,8,0,8)};var amount=Ui.Text(Display.Money(doc.WishlistAllocated(w.Id,b.Id)),14);DockPanel.SetDock(amount,Dock.Right);row.Children.Add(amount);var info=Ui.Stack();info.Children.Add(Ui.Text(b.Name,14));info.Children.Add(Ui.Text("Available "+Display.Money(doc.AvailableBalance(b.Id)),12,Ui.Muted));row.Children.Add(info);panel.Children.Add(new Border{Child=row,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)});
        }
        var historyTitle=Ui.Text("Activity",16,bold:true);historyTitle.Margin=new Thickness(0,20,0,8);panel.Children.Add(historyTitle);
        foreach(var e in doc.WishlistEvents.Select((activity,index)=>(activity,index)).Where(x=>x.activity.WishlistId==w.Id).OrderByDescending(x=>x.activity.Date).ThenByDescending(x=>x.index).Select(x=>x.activity)){
            var row=new Grid{Margin=new Thickness(0,8,0,8)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(88)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.Children.Add(Ui.Text(Display.Date(e.Date),12,Ui.Muted));var text=Ui.Text(WishlistPresentation.Description(e),12);Grid.SetColumn(text,1);row.Children.Add(text);
            var edit=Ui.Button("Edit Date",()=>EditWishlistActivityDate(e));edit.Margin=new Thickness(8,0,0,0);edit.ToolTip="Edit date: "+WishlistPresentation.ActivityName(e.Kind);Grid.SetColumn(edit,2);row.Children.Add(edit);
            var activityRow=new Border{Child=row,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,0,1)};wishlistActivityRows[e.Id]=activityRow;panel.Children.Add(activityRow);
        }
        if(status is WishlistStatus.Purchased or WishlistStatus.Closed){var correct=Ui.Button("Correct Purchase",()=>WishlistAction("correct"));correct.HorizontalAlignment=HorizontalAlignment.Left;correct.Margin=new Thickness(0,12,0,0);panel.Children.Add(correct);}
        if(!string.IsNullOrWhiteSpace(w.Notes)){var title=Ui.Text("Notes",16,bold:true);title.Margin=new Thickness(0,16,0,8);panel.Children.Add(title);panel.Children.Add(Ui.Text(w.Notes,14,Ui.Muted));}
        return panel;
    }
    private void WishlistAction(string action)
    {
        try{
            if(action=="new"){
                var dialog=new WishlistDetailsDialog(this,session);if(dialog.ShowDialog()==true){selectedWishlist=dialog.ResultId;selectedBank=null;page="Wishlist";}Render();return;
            }
            var w=ActiveWishlist;if(w is null)return;
            if(action=="edit"){new WishlistDetailsDialog(this,session,w).ShowDialog();Render();return;}
            if(action is "allocate" or "release" or "move" or "purchase"){
                FinanceDocument.Require(session.Document.Banks.Count>=(action=="move"?2:1),action=="move"?"Add two bank accounts before moving funds.":"Add a bank account first.");new WishlistFundsDialog(this,session,w,action).ShowDialog();Render();return;
            }
            var detail=string.Join("\n",session.Document.Banks.Where(b=>session.Document.WishlistAllocated(w.Id,b.Id)>0).Select(b=>b.Name+": "+Display.Money(session.Document.WishlistAllocated(w.Id,b.Id))));
            var (title,message,button)=action switch{
                "balance"=>("Release Balance?","Release the remaining allocations to available balance in the same accounts?\n\n"+detail,"Release Balance"),
                "cancel"=>("Cancel Wishlist?","Cancel this wishlist and release all its allocations? History is retained.\n\n"+detail,"Cancel Wishlist"),
                "close"=>("Close Wishlist?","Close this purchased wishlist? Purchase and allocation history are retained.","Close Wishlist"),
                _=>("Correct Purchase?","Reverse the linked expense and reopen as Planned. The last purchase amount returns to this wishlist's allocation. Previously released funds stay available. This corrects a recording error; it is not a store refund.","Reverse and Reopen")
            };
            if(!ConfirmDialog.Confirm(this,title,message,button))return;var today=DateOnly.FromDateTime(DateTime.Today);
            session.Apply(action switch{"balance"=>WishlistService.ReleaseBalance(session.Document,w.Id,today),"cancel"=>WishlistService.Cancel(session.Document,w.Id,today),"close"=>WishlistService.Close(session.Document,w.Id,today),_=>WishlistService.CorrectPurchase(session.Document,w.Id,today)});Render();
        }catch(InvalidDataException e){ConfirmDialog.Alert(this,"Wishlist",e.Message);Render();}
    }
    private bool RedirectWishlistTransaction(Transaction transaction,bool correct=false)
    {
        var activity=session.Document.WishlistEvents.FirstOrDefault(e=>e.TransactionId==transaction.Id&&e.Kind is WishlistEventKind.Purchased or WishlistEventKind.Transferred);if(activity is null)return false;
        selectedWishlist=activity.WishlistId;selectedBank=null;ShowWishlist();
        if(correct&&activity.Kind==WishlistEventKind.Purchased)WishlistAction("correct");
        else ConfirmDialog.Alert(this,"Wishlist-linked transaction",activity.Kind==WishlistEventKind.Purchased?"To correct the purchase date, use Edit Date beside Purchased in Wishlist Activity. For other purchase corrections, use Correct Purchase to reverse and reopen the item.":"To correct this transfer's date, use Edit Date beside Transferred in Wishlist Activity. Other transfer fields cannot be edited or deleted independently. Use Move Funds to record an actual return transfer.");return true;
    }
}

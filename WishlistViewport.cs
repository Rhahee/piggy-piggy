using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow
{
    private ScrollViewer? wishlistListScroll,wishlistDetailScroll;
    private Grid? wishlistSplit;
    private Guid? renderedWishlist;
    private double wishlistListOffset,wishlistLeftWidth=224;
    private readonly Dictionary<Guid,double> wishlistDetailOffsets=[];
    private Dictionary<Guid,FrameworkElement> wishlistActivityRows=[];
    private (Guid Id,double Top)? wishlistActivityAnchor;

    private void CaptureWishlistViewport()
    {
        if(wishlistListScroll?.IsLoaded==true)wishlistListOffset=wishlistListScroll.VerticalOffset;
        if(wishlistDetailScroll?.IsLoaded==true&&renderedWishlist is Guid id)wishlistDetailOffsets[id]=wishlistDetailScroll.VerticalOffset;
        if(wishlistSplit?.IsLoaded==true&&wishlistSplit.ColumnDefinitions[0].ActualWidth>0)wishlistLeftWidth=wishlistSplit.ColumnDefinitions[0].ActualWidth;
        wishlistListScroll=null;wishlistDetailScroll=null;wishlistSplit=null;
    }
    private void ResetWishlistViewport()
    {
        wishlistListScroll=null;wishlistDetailScroll=null;wishlistSplit=null;renderedWishlist=null;selectedWishlist=null;
        wishlistListOffset=0;wishlistLeftWidth=224;wishlistDetailOffsets.Clear();wishlistActivityRows.Clear();wishlistActivityAnchor=null;
    }
    private void EditWishlistActivityDate(WishlistEvent activity)
    {
        var viewer=wishlistDetailScroll;
        double? top=viewer?.IsLoaded==true&&wishlistActivityRows.TryGetValue(activity.Id,out var row)?row.TransformToAncestor(viewer).Transform(new Point()).Y:null;
        if(new WishlistActivityDateDialog(this,session,activity).ShowDialog()==true&&top is double y)wishlistActivityAnchor=(activity.Id,y);
        Render();
    }
    private void RestoreWishlistViewport(ScrollViewer list,ScrollViewer detail,Guid id,bool selectionChanged,FrameworkElement? selectedButton)
    {
        var anchor=wishlistActivityAnchor;wishlistActivityAnchor=null;var rows=wishlistActivityRows;
        detail.Loaded+=(_,_)=>Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
            if(!ReferenceEquals(wishlistDetailScroll,detail)||!detail.IsLoaded)return;
            list.ScrollToVerticalOffset(wishlistListOffset);
            if(selectionChanged)selectedButton?.BringIntoView();
            if(anchor is { } focus&&rows.TryGetValue(focus.Id,out var row)&&detail.Content is UIElement content){
                // Anchor by activity ID, so changing its date and sort position keeps the edited row visible.
                detail.ScrollToVerticalOffset(row.TranslatePoint(new Point(),content).Y-focus.Top);
            }else detail.ScrollToVerticalOffset(wishlistDetailOffsets.GetValueOrDefault(id));
        }));
    }
}

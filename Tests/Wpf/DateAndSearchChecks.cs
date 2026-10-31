using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MyPiggyBank;

internal static class DateAndSearchChecks
{
    private sealed record Row(decimal Amount,string Name);
    private static void Require(bool value,string message) { if(!value)throw new Exception(message); }
    private static Calendar? CalendarIn(DependencyObject root)
    {
        if(root is Calendar calendar)return calendar;
        for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
            if(CalendarIn(VisualTreeHelper.GetChild(root,i)) is {} result)return result;
        return null;
    }
    private static void Picker(Action<Calendar,TextBox,Button> body)
    {
        var input=FormWindow.DateInput(new DateOnly(2020,2,12));
        var content=(DockPanel)FormWindow.DatePickerInput(input);
        var button=content.Children.OfType<Button>().Single();
        var window=new Window{Content=content,Width=400,Height=140,ShowInTaskbar=false};
        Exception? error=null;
        window.Loaded+=(_,_)=>window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                try {
                    var calendar=PresentationSource.CurrentSources.Cast<PresentationSource>()
                        .Select(s=>s.RootVisual).Where(v=>v is not null).Select(v=>CalendarIn(v)).FirstOrDefault(c=>c is not null);
                    Require(calendar is not null,"The production calendar popup did not open.");
                    body(calendar!,input,button);
                    calendar!.SelectedDate=new DateTime(2020,2,13);
                } catch(Exception failure){error=failure;}
                finally{window.Close();}
            }));
        }));
        window.ShowDialog();if(error is not null)throw error;
    }
    public static int Run()
    {
        var count=0;
        void Check(string name,Action body){body();count++;Console.WriteLine("PASS "+name);}
        Check("minimum-size workspace keeps empty message visible and Ctrl+F focuses search",()=>{
            var window=new MainWindow{Width=1000,Height=640,ShowInTaskbar=false};
            Exception? failure=null;
            window.Loaded+=(_,_)=>window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                System.Windows.Input.ApplicationCommands.New.Execute(null,window);
                System.Windows.Input.ApplicationCommands.Find.Execute(null,window);
                window.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{ try { window.UpdateLayout();
                var search=(TextBox)typeof(MainWindow).GetField("searchBox",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
                Require(search.IsKeyboardFocusWithin,"Ctrl+F did not focus the search field.");
                var ledger=(DataGrid)typeof(MainWindow).GetField("ledger",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(window)!;
                var surface=(Grid)ledger.Parent;var empty=(Border)surface.Children[1];
                var bounds=empty.TransformToAncestor(window).TransformBounds(new Rect(0,0,empty.ActualWidth,empty.ActualHeight));
                Require(empty.Visibility==Visibility.Visible&&bounds.Width>0&&bounds.Height>0&&bounds.Left>=0&&bounds.Top>=0&&bounds.Right<=window.ActualWidth&&bounds.Bottom<=window.ActualHeight,"Empty message was clipped at minimum size.");
            } catch(Exception error){failure=error;} finally { window.Close(); } }));
            }));window.ShowDialog();if(failure is not null)throw failure;
        });
        Check("date popup displays the historical input month without editing the input",()=>Picker((calendar,input,button)=>{
            Require(calendar.DisplayDate.Year==2020&&calendar.DisplayDate.Month==2,"Calendar displayed the wrong month.");
            Require(calendar.SelectedDate==new DateTime(2020,2,12)&&input.Text=="02/12/2020","Opening changed the date.");
            calendar.DisplayMode=CalendarMode.Year;calendar.DisplayDate=new DateTime(2024,6,1);
            input.Text="09/25/2021";button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(calendar.DisplayMode==CalendarMode.Month&&calendar.DisplayDate.Year==2021&&calendar.DisplayDate.Month==9,"Reopening retained stale navigation.");
            Require(input.Text=="09/25/2021","Reopening changed the input.");
        }));
        Check("invalid or blank date input opens today without stale selection or text replacement",()=>Picker((calendar,input,button)=>{
            foreach(var text in new[]{"invalid",""}){
                input.Text=text;button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Require(calendar.SelectedDate is null&&calendar.DisplayDate.Date==DateTime.Today,"Invalid input retained a stale date.");
                Require(input.Text==text,"Opening replaced invalid or empty text.");
            }
        }));
        Check("search refresh preserves numeric sorting arrows and empty-result recovery",()=>{
            var table=new DataGrid{AutoGenerateColumns=false};table.Columns.Add(new DataGridTextColumn{SortMemberPath="Amount"});
            table.ItemsSource=new[]{new Row(200,"B"),new Row(100,"A")};
            table.Items.SortDescriptions.Add(new("Amount",ListSortDirection.Ascending));table.Columns[0].SortDirection=ListSortDirection.Ascending;
            var surface=Ui.LedgerSurface(table,"Empty","Adjust filters");
            Ui.RefreshLedger(table,Array.Empty<Row>());
            Require(!table.HasItems&&table.Items.SortDescriptions.Count==1,"Empty results discarded the sort.");
            surface.Measure(new Size(600,200));surface.Arrange(new Rect(0,0,600,200));surface.UpdateLayout();
            Require(((Border)surface.Children[1]).Visibility==Visibility.Visible,"Empty message stayed hidden.");
            Ui.RefreshLedger(table,new[]{new Row(200,"B"),new Row(100,"A")});surface.UpdateLayout();
            Require(table.Items.Cast<Row>().Select(r=>r.Amount).SequenceEqual(new decimal[]{100,200}),"Numeric sorting was lost.");
            Require(table.Columns[0].SortDirection==ListSortDirection.Ascending,"Sort arrow was lost.");
            Require(((Border)surface.Children[1]).Visibility==Visibility.Collapsed,"Empty message covered matching rows.");
            table.Items.SortDescriptions.Clear();table.Items.SortDescriptions.Add(new("Amount",ListSortDirection.Descending));table.Columns[0].SortDirection=ListSortDirection.Descending;
            Ui.RefreshLedger(table,new[]{new Row(100,"A"),new Row(200,"B")});
            Require(table.Items.Cast<Row>().First().Amount==200&&table.Columns[0].SortDirection==ListSortDirection.Descending,"Descending sorting was lost.");
        });
        return count;
    }
}

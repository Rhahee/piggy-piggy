using MyPiggyBank;
using MyPiggyBank.Core;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Automation;
using System.Net;
using System.Net.Http;
using System.Reflection;

internal static class Program
{
    private static int passed;
    private static readonly DateOnly Start=new(2026,10,1);
    private static readonly Bank From=new(Guid.NewGuid(),"OCBC",Start,2_000_000),To=new(Guid.NewGuid(),"BCA",Start,3_000_000);
    private static readonly Purpose Purpose=new(Guid.NewGuid(),"Pocket money");
    private static readonly Debt Existing=new(Guid.NewGuid(),"Existing pocket","",null,Kind:DebtKind.Self,SelfBankId:From.Id);
    private static Transaction Borrow()=>new(Guid.NewGuid(),new(2026,10,4),TransactionKind.Transfer,From.Id,To.Id,null,500_000,2500,null,"Pocket",[new(Purpose.Id,500_000,Existing.Id,SelfDebtAction.Borrow)]);
    private static DocumentSession Session()
    {
        var s=new DocumentSession();s.Apply(new FinanceDocument{Banks=[From,To],Purposes=[Purpose],Debts=[Existing],Transactions=[Borrow()]});return s;
    }
    private static IEnumerable<T> Desc<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T hit)yield return hit;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Desc<T>(child))yield return item;
    }
    private static ComboBox Mode(AllocationEditor editor,int row=0)=>Desc<ComboBox>(editor).Where(c=>c.Items.Cast<object>().Any(i=>i is string s&&s=="Borrow as Self Debt")).ElementAt(row);
    private static ComboBox Debt(AllocationEditor editor,int row=0)=>Desc<ComboBox>(editor).Where(c=>c.DisplayMemberPath=="Name"&&!c.IsEditable).ElementAt(row);
    private static string Label(object? value)=>value?.GetType().GetProperty("Name")?.GetValue(value)?.ToString()??"";
    private static void Check(string name,Action body){body();passed++;Console.WriteLine("PASS "+name);}
    private static void Equal<T>(T expected,T actual){if(!EqualityComparer<T>.Default.Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
    private static Border LedgerCell(DataGridTemplateColumn column,LedgerRow row)
    {
        var presenter=new ContentPresenter{Content=row,ContentTemplate=column.CellTemplate};
        presenter.Measure(new Size(column.Width.Value,32));presenter.Arrange(new Rect(0,0,column.Width.Value,32));presenter.UpdateLayout();
        return (Border)VisualTreeHelper.GetChild(presenter,0);
    }
    [STAThread]
    public static int Main()
    {
        var app=new App();app.InitializeComponent();var owner=new Window();new System.Windows.Interop.WindowInteropHelper(owner).EnsureHandle();
        Check("Purpose only begins without debt metadata",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,null);e.AddRow(Purpose.Id,500_000);Equal(0,Mode(e).SelectedIndex);var result=e.Read();Equal(0,result.NewDebts.Count);Equal<Guid?>(null,result.Allocations.Single().SelfDebtId);});
        Check("Borrow new stages name due date and original bank without mutating session",()=>{var s=Session();var before=JsonSerializer.Serialize(s.Document);var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,null);e.AddRow(Purpose.Id,500_000);Mode(e).SelectedIndex=1;Equal("New Self Debt",Label(Debt(e).SelectedItem));Desc<TextBox>(e).Single(t=>t.MaxLength==100).Text="New borrowing";Desc<TextBox>(e).Single(t=>t.MaxLength==10).Text="12/31/2026";var r=e.Read();Equal(1,r.NewDebts.Count);Equal("New borrowing",r.NewDebts.Single().Name);Equal<Guid?>(From.Id,r.NewDebts.Single().SelfBankId);Equal(new DateOnly(2026,12,31),(DateOnly)r.NewDebts.Single().DueDate!);Equal(SelfDebtAction.Borrow,r.Allocations.Single().DebtAction);Equal(before,JsonSerializer.Serialize(s.Document));var tx=Borrow() with{Allocations=r.Allocations};var d=DebtService.SaveTransaction(s.Document,tx,additionalDebts:r.NewDebts);Equal(2,d.Debts.Count);});
        Check("existing borrowing loads Borrow rather than Repay",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,s.Document.Transactions.Single().Allocations);Equal(1,Mode(e).SelectedIndex);var r=e.Read();Equal(0,r.NewDebts.Count);Equal<Guid?>(Existing.Id,r.Allocations.Single().SelfDebtId);Equal(SelfDebtAction.Borrow,r.Allocations.Single().DebtAction);});
        Check("old repayment loads Repay with no new name fields",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>200_000,()=>To.Id,[new(Purpose.Id,200_000,Existing.Id)]);Equal(2,Mode(e).SelectedIndex);Equal(SelfDebtAction.Repay,e.Read().Allocations.Single().DebtAction);Equal(Visibility.Collapsed,(Desc<TextBox>(e).Single(t=>t.MaxLength==100).Parent as FrameworkElement)!.Visibility);});
        Check("switch to Purpose only drops link and action while keeping amount",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,s.Document.Transactions.Single().Allocations);Mode(e).SelectedIndex=0;var r=e.Read();Equal<Guid?>(null,r.Allocations.Single().SelfDebtId);Equal(SelfDebtAction.Repay,r.Allocations.Single().DebtAction);Equal(500_000m,r.Allocations.Single().Amount);});
        Check("Borrow existing selects stored identity without new metadata",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>100_000,()=>From.Id,null);e.AddRow(Purpose.Id,100_000);Mode(e).SelectedIndex=1;Debt(e).SelectedItem=Debt(e).Items.Cast<object>().Single(i=>Label(i).Contains("Existing pocket"));var r=e.Read();Equal(0,r.NewDebts.Count);Equal<Guid?>(Existing.Id,r.Allocations.Single().SelfDebtId);});
        Check("new borrowing source follows From Bank before submission",()=>{var s=Session();Guid? from=From.Id;var e=new AllocationEditor(owner,s,()=>500_000,()=>from,null);e.AddRow(Purpose.Id,500_000);Mode(e).SelectedIndex=1;Desc<TextBox>(e).Single(t=>t.MaxLength==100).Text="Other source";from=To.Id;e.UpdateTotals();Equal<Guid?>(To.Id,e.Read().NewDebts.Single().SelfBankId);});
        Check("invalid due date leaves session unchanged",()=>{var s=Session();var before=JsonSerializer.Serialize(s.Document);var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,null);e.AddRow(Purpose.Id,500_000);Mode(e).SelectedIndex=1;Desc<TextBox>(e).Single(t=>t.MaxLength==10).Text="bad date";try{e.Read();throw new Exception("Expected rejection");}catch(System.IO.InvalidDataException){}Equal(before,JsonSerializer.Serialize(s.Document));});
        Check("new transfer preset selects Borrow New",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,null,borrowNew:true);Equal(1,Mode(e).SelectedIndex);Equal("New Self Debt",Label(Debt(e).SelectedItem));});
        Check("repayment preset retains existing debt",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>200_000,()=>To.Id,null,Existing.Id);Equal(2,Mode(e).SelectedIndex);Equal("Existing pocket · Self Debt · OCBC",Label(Debt(e).SelectedItem));});
        Check("transaction borrowing preset uses original bank as From Bank",()=>{var s=Session();var dialog=new TransactionDialog(owner,s,TransactionKind.Transfer,debtPreset:Existing.Id,trackNewDebt:true);var banks=Desc<ComboBox>(dialog).Where(c=>c.SelectedItem is Bank).ToList();Equal(From.Id,((Bank)banks[0].SelectedItem).Id);Equal(To.Id,((Bank)banks[1].SelectedItem).Id);dialog.Close();});
        Check("transaction repayment preset uses original bank as To Bank",()=>{var s=Session();var dialog=new TransactionDialog(owner,s,TransactionKind.Transfer,debtPreset:Existing.Id);var banks=Desc<ComboBox>(dialog).Where(c=>c.SelectedItem is Bank).ToList();Equal(To.Id,((Bank)banks[0].SelectedItem).Id);Equal(From.Id,((Bank)banks[1].SelectedItem).Id);dialog.Close();});
        Check("expanded transfer editor measures within supported content width",()=>{var s=Session();var e=new AllocationEditor(owner,s,()=>500_000,()=>From.Id,null,borrowNew:true);e.Measure(new Size(500,double.PositiveInfinity));e.Arrange(new Rect(0,0,500,e.DesiredSize.Height));Equal(true,e.DesiredSize.Height>0&&e.DesiredSize.Width<=500);});
        Check("ledger type icons retain spoken labels and distinguish all transaction types including fees",()=>
        {
            var doc=Session().Document;
            var rows=new[]{Borrow(),Borrow() with{Kind=TransactionKind.Income,ToBankId=null,Allocations=null},Borrow() with{Kind=TransactionKind.Expense,ToBankId=null,Allocations=null}}.SelectMany(t=>LedgerRow.From(doc,t)).ToArray();
            foreach(var row in rows)
            {
                var cell=LedgerCell(Ui.TypeColumn(),row);
                Equal(row.Type,AutomationProperties.GetName(cell));Equal(row.Type,((TextBlock)cell.ToolTip).Text);
                Equal(row.TypeImage,((Image)cell.Child).Source);Equal(0,Desc<TextBlock>(cell).Count());
                Equal(row.Type,AutomationProperties.GetName((Image)cell.Child));
            }
            Equal(3,rows.Where(r=>!r.IsFee).Select(r=>r.Type).Distinct().Count());
        });
        Check("allocation and debt icons hide empty cells but retain complete searchable tooltip details",()=>
        {
            var doc=Session().Document;var rows=LedgerRow.From(doc,Borrow()).ToArray();
            foreach(var row in rows.Append(LedgerRow.From(doc,Borrow() with{Allocations=null,Fee=0}).Single()))
            foreach(var column in new[]{Ui.AllocationColumn(),Ui.DebtColumn()})
            {
                var allocation=column.SortMemberPath=="Allocation";var present=allocation?row.HasAllocation:row.HasDebt;
                var cell=LedgerCell(column,row);
                Equal(present?Visibility.Visible:Visibility.Collapsed,cell.Visibility);
                Equal(allocation?row.Allocation:row.DebtTooltip,((TextBlock)cell.ToolTip).Text);
                Equal(allocation?row.Allocation:row.Debt,AutomationProperties.GetName(cell));
            }
            Equal(true,rows[0].Allocation.Contains("Pocket money"));Equal(true,rows[0].Debt.Contains("Existing pocket"));
            Equal(false,rows[1].HasAllocation);Equal(true,rows[1].DebtTooltip.Contains("does not change debt principal"));
        });
        Check("production ledger keeps compact columns, sorting and a larger description share",()=>
        {
            var window=new MainWindow();
            var make=typeof(MainWindow).GetMethod("MakeLedger",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            var grid=(DataGrid)make.Invoke(window,[true])!;grid.Style=(Style)app.FindResource(typeof(DataGrid));grid.ItemsSource=LedgerRow.From(Session().Document,Borrow()).ToList();
            foreach(var pair in new[]{(1,56.0),(5,48.0),(6,48.0)}){Equal(pair.Item2,grid.Columns[pair.Item1].Width.Value);Equal(false,grid.Columns[pair.Item1].CanUserResize);}
            Equal(true,grid.Columns[4].Width.IsStar&&grid.Columns[4].Width.Value>grid.Columns[2].Width.Value);
            Equal("Type",grid.Columns[1].SortMemberPath);
            Equal("Allocation",grid.Columns[5].SortMemberPath);Equal("Debt",grid.Columns[6].SortMemberPath);
            Equal("Allocations",AutomationProperties.GetName((Image)grid.Columns[5].Header));
            var recent=(DataGrid)make.Invoke(window,[false])!;Equal(5,recent.Columns.Count);Equal(56.0,recent.Columns[1].Width.Value);
            window.Close();
        });
        Check("long ledger text uses ellipsis and keeps the complete tooltip",()=>
        {
            var row=LedgerRow.From(Session().Document,Borrow() with{Description="A long purchase description that must remain readable even in a narrow column"}).First();
            var column=Ui.Column("Description","Description",2.4);
            var text=new TextBlock{DataContext=row,Style=column.ElementStyle,Width=100};
            text.SetBinding(TextBlock.TextProperty,column.Binding);text.Measure(new Size(100,32));
            Equal(TextTrimming.CharacterEllipsis,text.TextTrimming);Equal(TextWrapping.NoWrap,text.TextWrapping);
            Equal(row.Description,text.Text);Equal(row.Description,((TextBlock)text.ToolTip).Text);
        });
        Check("money search accepts grouped ungrouped currency and decimals without mixing fee and principal",()=>
        {
            var doc=Session().Document;
            var income=Borrow() with{Kind=TransactionKind.Income,ToBankId=null,Allocations=null,Amount=97_500.25m,IncomeBasis=IncomeAmountBasis.AfterFee};
            var rows=LedgerRow.From(doc,income).ToArray();
            foreach(var term in new[]{"100000.25","100,000.25"," Rp 100,000.25 "})
            {
                var result=rows.Where(LedgerRow.Search(term)).ToArray();
                Equal(1,result.Length);Equal(false,result[0].IsFee);
            }
            foreach(var term in new[]{"2500","2,500.00","Rp 2,500"})Equal(true,rows.Where(LedgerRow.Search(term)).Single().IsFee);
            var whole=LedgerRow.From(doc,income with{Amount=100_000,Fee=0,IncomeBasis=IncomeAmountBasis.BeforeFee}).Single();
            foreach(var term in new[]{"100000","100,000.00","100000.0","rp 100000.00"})Equal(true,LedgerRow.Search(term)(whole));
            foreach(var term in new[]{"99999","1.000,00","1e5"})Equal(false,LedgerRow.Search(term)(whole));
            Equal(false,rows.Any(LedgerRow.Search("100000.250")));
        });
        Check("money search retains date type description Purpose debt and fee text searches",()=>
        {
            var rows=LedgerRow.From(Session().Document,Borrow()).ToArray();
            foreach(var term in new[]{"  pocket MONEY  ","Self Debt","OCBC","10/04/2026","Transfer",""})Equal(true,rows.Any(LedgerRow.Search(term)));
            Equal(true,rows.Where(LedgerRow.Search("Admin Fee")).Single().IsFee);
            Equal(false,rows.Any(LedgerRow.Search("not present anywhere")));
        });
        Check("update session rechecks after an available result and recovers from a failed recheck",()=>
        {
            var calls=0;
            var epoch=int.Parse(typeof(MainWindow).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a=>a.Key=="UpdateEpoch").Value!);
            var prefix=AppVersion.TagPrefix(epoch,new Version(9,0,0,0));
            var handler=new WpfUpdateHttpStub(request=>
            {
                if(request.RequestUri==ReleaseChecker.ChannelEndpoint)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{schema=1,epoch,version=$"9.0.0.{calls+1}",tag=$"{prefix}9.0.0.{calls+1}"}))};
                }
                Equal($"https://api.github.com/repos/Rhahee/piggy-piggy/releases/tags/{prefix}9.0.0.{calls+1}",request.RequestUri!.AbsoluteUri);calls++;
                if(calls==3)return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                var version=$"9.0.0.{calls}";var name=$"Piggy-Piggy-{version}-win-x64.zip";
                return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{tag_name=prefix+version,draft=false,prerelease=false,assets=new[]{new{name,size=1,browser_download_url=$"https://github.com/Rhahee/piggy-piggy/releases/download/{prefix}{version}/{name}"}}}))};
            });
            var type=typeof(MainWindow).Assembly.GetType("MyPiggyBank.UpdateSession")!;
            using var session=(IDisposable)type.GetConstructor(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[typeof(HttpClient)],null)!.Invoke([new HttpClient(handler)]);
            var activate=type.GetMethod("ActivateAsync")!.CreateDelegate<Func<Window,Task>>(session);
            var closedOwner=new Window();new System.Windows.Interop.WindowInteropHelper(closedOwner).EnsureHandle();closedOwner.Close();
            for(var attempt=1;attempt<=4;attempt++)
            {
                activate(closedOwner).GetAwaiter().GetResult();Equal(attempt,calls);
                Equal(false,(bool)type.GetProperty("Checking")!.GetValue(session)!);
                var result=(UpdateCheck)type.GetProperty("Result")!.GetValue(session)!;
                Equal(attempt==3?UpdateStatus.Unavailable:UpdateStatus.Available,result.Status);
                if(attempt!=3)Equal(new Version(9,0,0,attempt),result.Release!.Version);
            }
        });
        owner.Close();Console.WriteLine($"All {passed} isolated WPF checks passed.");return 0;
    }
}

internal sealed class WpfUpdateHttpStub(Func<HttpRequestMessage,HttpResponseMessage> respond):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(respond(request));
}

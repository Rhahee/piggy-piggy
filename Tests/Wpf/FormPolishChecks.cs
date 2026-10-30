using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MyPiggyBank;
using MyPiggyBank.Core;

internal static class FormPolishChecks
{
    private static readonly DateOnly Start = new(2026,10,1);
    private static DocumentSession Session()
    {
        var session = new DocumentSession();
        session.Apply(new FinanceDocument {
            Banks = [new(Guid.NewGuid(),"QA A",Start,1_000_000),new(Guid.NewGuid(),"QA B",Start,1_000_000)],
            Categories = [new(Guid.NewGuid(),"Salary",CategoryKind.Income),new(Guid.NewGuid(),"Shopping",CategoryKind.Expense)]
        });
        return session;
    }
    private static IEnumerable<T> Desc<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T found) yield return found;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach (var value in Desc<T>(child)) yield return value;
    }
    private static T Field<T>(FormWindow form,string label) where T : DependencyObject
    {
        var row = Desc<Grid>(form).Single(g => g.Children.OfType<TextBlock>().Any(t => t.Text == label));
        return Desc<T>(row).Single();
    }
    private static void Require(bool condition,string message) { if (!condition) throw new Exception(message); }
    private static void Focused(FrameworkElement input,FormWindow form)
        => Require(input.IsKeyboardFocusWithin || ReferenceEquals(FocusManager.GetFocusedElement(form),input),"Validation did not focus the invalid input.");
    private static void Submit(FormWindow form)
    {
        var submit = Desc<Button>(form).Single(b => b.IsDefault);
        submit.Focus(); submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    private static void InDialog(FormWindow form,Action body)
    {
        Exception? failure = null;
        form.ShowInTaskbar = false;
        form.Loaded += (_,_) => form.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() => {
            try { body(); }
            catch (Exception error) { failure = error; }
            finally { if (form.IsVisible) form.Close(); }
        }));
        form.ShowDialog();
        if (failure is not null) throw new Exception("Form regression check failed.",failure);
    }
    private static void ValidTransaction(FormWindow form,TransactionKind kind)
    {
        Field<TextBox>(form,"Date *").Text="10/04/2026";
        Field<MoneyTextBox>(form,"Amount (Rp) *").Text="100";
        if (kind != TransactionKind.Transfer) Field<ComboBox>(form,"Category *").SelectedIndex=0;
    }
    private static FormWindow InternalForm(string name,params object?[] args)
        => (FormWindow)Activator.CreateInstance(typeof(MainWindow).Assembly.GetType("MyPiggyBank."+name)!,args)!;

    public static int Run(Window owner)
    {
        var count=0;
        void Check(string name,Action body) { body(); count++; Console.WriteLine("PASS "+name); }
        Check("blank bank name receives validation focus without creating a bank",() => {
            var session=Session();var before=JsonSerializer.Serialize(session.Document);var form=new BankDialog(owner,session.Document);
            InDialog(form,()=>{Submit(form);Focused(Field<TextBox>(form,"Bank Name *"),form);Require(form.Result is null,"Rejected bank was staged.");});
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected bank changed the document.");
        });
        Check("duplicate bank name focuses its name input",() => {
            var form=new BankDialog(owner,Session().Document);
            InDialog(form,()=>{Field<TextBox>(form,"Bank Name *").Text="qa a";Submit(form);Focused(Field<TextBox>(form,"Bank Name *"),form);Require(form.Result is null,"Duplicate bank accepted.");});
        });
        Check("blank opening balance still creates a zero-balance bank",()=>{
            var form=new BankDialog(owner,Session().Document);
            InDialog(form,()=>{Field<TextBox>(form,"Bank Name *").Text="QA Zero";Submit(form);});
            Require(form.Result is {OpeningBalance:0},"Blank opening balance stopped meaning zero.");
        });
        Check("duplicate category focuses its name input",() => {
            var form=new CategoryDialog(owner,Session().Document);
            InDialog(form,()=>{Field<TextBox>(form,"Category Name *").Text="shopping";Submit(form);Focused(Field<TextBox>(form,"Category Name *"),form);Require(form.Result is null,"Duplicate category accepted.");});
        });
        Check("required purpose focuses its name input",() => {
            var form=new PurposeDialog(owner,Session().Document);
            InDialog(form,()=>{Submit(form);Focused(Field<TextBox>(form,"Purpose Name *"),form);Require(form.Result is null,"Blank purpose accepted.");});
        });
        void RejectedTransaction(string name,TransactionKind kind,Action<TransactionDialog> configure,string invalidLabel,bool combo=false)
            => Check(name,()=>{
                var session=Session();var before=JsonSerializer.Serialize(session.Document);var dirty=session.Dirty;
                var form=new TransactionDialog(owner,session,kind);
                InDialog(form,()=>{ValidTransaction(form,kind);configure(form);Submit(form);Focused(combo?Field<ComboBox>(form,invalidLabel):Field<TextBox>(form,invalidLabel),form);});
                Require(before==JsonSerializer.Serialize(session.Document)&&dirty==session.Dirty,"Rejected transaction changed the session.");
            });
        RejectedTransaction("invalid transaction date receives focus",TransactionKind.Expense,f=>Field<TextBox>(f,"Date *").Text="13/40/2026","Date *");
        RejectedTransaction("date before opening balance receives focus",TransactionKind.Expense,f=>Field<TextBox>(f,"Date *").Text="09/30/2026","Date *");
        RejectedTransaction("missing category focuses the category selector",TransactionKind.Expense,f=>Field<ComboBox>(f,"Category *").SelectedIndex=-1,"Category *",true);
        RejectedTransaction("zero transaction amount receives focus",TransactionKind.Expense,f=>Field<MoneyTextBox>(f,"Amount (Rp) *").Text="0","Amount (Rp) *");
        RejectedTransaction("invalid fee receives focus without changing accounting",TransactionKind.Expense,f=>Field<MoneyTextBox>(f,"Admin Fee (Rp)").Text="-1","Admin Fee (Rp)");
        RejectedTransaction("gross income fee above the amount receives focus",TransactionKind.Income,f=>Field<MoneyTextBox>(f,"Admin Fee (Rp)").Text="101","Admin Fee (Rp)");
        RejectedTransaction("same-bank transfer focuses the destination selector",TransactionKind.Transfer,f=>Field<ComboBox>(f,"To Bank *").SelectedItem=Field<ComboBox>(f,"From Bank *").SelectedItem,"To Bank *",true);
        Check("new debt required input scrolls into view without leaving staged debt",()=>{
            var session=Session();var before=JsonSerializer.Serialize(session.Document);
            var form=new TransactionDialog(owner,session,TransactionKind.Expense,trackNewDebt:true){SizeToContent=SizeToContent.Manual,Height=480};
            InDialog(form,()=>{
                ValidTransaction(form,TransactionKind.Expense);Submit(form);var field=Field<TextBox>(form,"Debt Name *");Focused(field,form);
                form.UpdateLayout();Require(Desc<ScrollViewer>(form).Any(s=>s.VerticalOffset>0),"Invalid debt input was not scrolled into view.");
            });
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected debt left staged data.");
        });
        Check("new lender debt focuses the lender input",()=>{
            var session=Session();var before=JsonSerializer.Serialize(session.Document);var form=new TransactionDialog(owner,session,TransactionKind.Income,trackNewDebt:true);
            InDialog(form,()=>{ValidTransaction(form,TransactionKind.Income);Field<TextBox>(form,"Debt Name *").Text="QA loan";Submit(form);Focused(Field<TextBox>(form,"Lender *"),form);});
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected lender debt left staged data.");
        });
        Check("invalid optional debt due date focuses the date input",()=>{
            var session=Session();var before=JsonSerializer.Serialize(session.Document);var form=new TransactionDialog(owner,session,TransactionKind.Income,trackNewDebt:true);
            InDialog(form,()=>{
                ValidTransaction(form,TransactionKind.Income);Field<TextBox>(form,"Debt Name *").Text="QA loan";Field<TextBox>(form,"Lender *").Text="QA lender";
                Field<TextBox>(form,"Due Date").Text="01/01/0001";Submit(form);Focused(Field<TextBox>(form,"Due Date"),form);
            });
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected due date left staged data.");
        });
        Check("Self Debt repayment to a different bank focuses the bank selector",()=>{
            var session=Session();var doc=session.Document.Copy();var debt=new Debt(Guid.NewGuid(),"QA Self Debt","",null,Kind:DebtKind.Self,SelfBankId:doc.Banks[0].Id);
            doc.Debts.Add(debt);doc.Transactions.Add(new(Guid.NewGuid(),Start,TransactionKind.Expense,doc.Banks[0].Id,null,doc.Categories.Single(c=>c.Kind==CategoryKind.Expense).Id,500,0,null,"QA origin",DebtId:debt.Id));
            session.Apply(doc);var before=JsonSerializer.Serialize(session.Document);var form=new TransactionDialog(owner,session,TransactionKind.Income,debtPreset:debt.Id);
            InDialog(form,()=>{
                ValidTransaction(form,TransactionKind.Income);Field<ComboBox>(form,"Bank *").SelectedItem=doc.Banks[1];Field<TextBox>(form,"Repayment\nAmount (Rp) *").Text="100";
                Submit(form);Focused(Field<ComboBox>(form,"Bank *"),form);
            });
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected repayment changed accounting.");
        });
        Check("invalid period year focuses the year input",()=>{
            var current=new ReportPeriod(ReportPeriodMode.CalendarMonth,Start,Start,Start.AddDays(30));var form=new PeriodDialog(owner,Session().Document,current);
            InDialog(form,()=>{Field<TextBox>(form,"Year *").Text="bad";Submit(form);Focused(Field<TextBox>(form,"Year *"),form);});
        });
        Check("custom date range focuses end date and ignores hidden year input",()=>{
            var current=new ReportPeriod(ReportPeriodMode.CalendarMonth,Start,Start,Start.AddDays(30));var form=new PeriodDialog(owner,Session().Document,current);
            InDialog(form,()=>{
                Field<TextBox>(form,"Year *").Text="bad";Field<ComboBox>(form,"Period type *").SelectedIndex=2;
                Field<TextBox>(form,"Start date *").Text="10/10/2026";Field<TextBox>(form,"End date *").Text="10/01/2026";
                Submit(form);Focused(Field<TextBox>(form,"End date *"),form);
            });
        });
        Check("zero wishlist target focuses its amount without creating wishlist",()=>{
            var session=Session();var before=JsonSerializer.Serialize(session.Document);var form=InternalForm("WishlistDetailsDialog",owner,session,null);
            InDialog(form,()=>{Field<TextBox>(form,"Name *").Text="QA goal";Field<MoneyTextBox>(form,"Target Amount\n(Rp) *").Text="0";Submit(form);Focused(Field<TextBox>(form,"Target Amount\n(Rp) *"),form);});
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected wishlist left staged data.");
        });
        Check("wishlist activity history rejection focuses its date",()=>{
            var session=Session();var wish=new WishlistItem(Guid.NewGuid(),"QA goal",500,null,WishlistPriority.Normal,"");
            var doc=WishlistService.Create(session.Document,wish,Start);doc=WishlistService.Allocate(doc,wish.Id,doc.Banks[0].Id,100,Start);
            session.Apply(doc);var before=JsonSerializer.Serialize(session.Document);var activity=doc.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Allocated);
            var form=InternalForm("WishlistActivityDateDialog",owner,session,activity);
            InDialog(form,()=>{Field<TextBox>(form,"Date *").Text=Display.Date(DateOnly.FromDateTime(DateTime.Today).AddDays(1));Submit(form);Focused(Field<TextBox>(form,"Date *"),form);});
            Require(before==JsonSerializer.Serialize(session.Document),"Rejected activity correction changed history.");
        });
        return count;
    }
}

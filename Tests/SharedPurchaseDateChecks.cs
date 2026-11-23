using MyPiggyBank.Core;
using System.Text.Json;

internal static class SharedPurchaseDateChecks
{
    private static readonly DateOnly Funded = new(2026, 11, 1), Bought = new(2026, 11, 2), Later = new(2026, 11, 3);
    private static readonly Bank A = new(Guid.NewGuid(), "A", Funded, 1_000_000), B = new(Guid.NewGuid(), "B", Funded, 1_000_000);
    private static readonly Category Expense = new(Guid.NewGuid(), "Shopping", CategoryKind.Expense), Income = new(Guid.NewGuid(), "Income", CategoryKind.Income);
    private static readonly WishlistGoal Goal = new(Guid.NewGuid(), "Camera", 500_000);
    private static readonly WishlistItem Budget = new(Guid.NewGuid(), "Shared Budget", 0, null, WishlistPriority.Normal, "", [Goal]);
    private static FinanceDocument Fixture(decimal price = 300_000, decimal otherAccount = 200_000, bool feeNeedsIncome = false)
    {
        var doc = new FinanceDocument { Banks = [feeNeedsIncome ? A with { OpeningBalance = 500_000 } : A, B], Categories = [Expense, Income] };
        doc = WishlistService.Create(doc, Budget, Funded);
        foreach (var (bank, amount) in new[] { (A, 500_000m), (B, otherAccount) }.Where(x => x.Item2 > 0))
        {
            doc = WishlistService.Allocate(doc, Budget.Id, bank.Id, amount, Funded);
            doc = WishlistService.AssignFunds(doc, Budget.Id, null, Budget.Id, Goal.Id, bank.Id, amount, Funded);
        }
        if (feeNeedsIncome) doc.Transactions.Add(new(Guid.NewGuid(), Bought, TransactionKind.Income, A.Id, null, Income.Id, 1000, 0, null, "Fee funding"));
        return WishlistService.Purchase(doc, Budget.Id, A.Id, Expense.Id, price, 1000, Bought, Goal.Id);
    }
    private static WishlistEvent Purchase(FinanceDocument doc) => doc.WishlistEvents.Single(e => e.Kind == WishlistEventKind.Purchased);
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Reject(Action action) { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Expected rejection"); }

    public static void Run(Action<string, Action> test)
    {
        test("shared purchase date moves its two-account surplus and expense together without mutating the source", () => {
            var source = Fixture(); var before = JsonSerializer.Serialize(source); var purchase = Purchase(source);
            foreach (var date in new[] { Later, Funded })
            {
                var changed = WishlistService.EditPurchaseDate(source, purchase.Id, date);
                Equal(before, JsonSerializer.Serialize(source));
                var group = source.WishlistEvents.SkipWhile(e => e.Id != purchase.Id).ToArray();
                Equal(5, group.Length);
                foreach (var activity in source.WishlistEvents)
                    Equal(group.Contains(activity) ? activity with { Date = date } : activity, changed.WishlistEvents.Single(e => e.Id == activity.Id));
                Equal(source.Transactions.Single() with { Date = date }, changed.Transactions.Single());
                Equal(0m, changed.WishlistBucket(Budget.Id, Goal.Id));
                Equal(200_000m, changed.WishlistBucket(Budget.Id, null, A.Id));
                Equal(200_000m, changed.WishlistBucket(Budget.Id, null, B.Id));
            }
            var later = WishlistService.EditPurchaseDate(source, purchase.Id, Later);
            Equal(1_000_000m, later.Balance(A.Id, Bought));
            Equal(500_000m, later.WishlistBucket(Budget.Id, Goal.Id, A.Id, Bought));
            Equal(200_000m, later.WishlistBucket(Budget.Id, Goal.Id, B.Id, Bought));
            Equal(0m, later.WishlistBucket(Budget.Id, null, null, Bought));
            Equal(301_000m, later.Totals(Later, Later).Expense);
        });
        test("purchase date guidance skips automatic surplus but respects the next real reallocation", () => {
            var doc = Fixture(); var purchase = Purchase(doc);
            Equal(new WishlistDateLimits(Funded, Later), WishlistPresentation.DateLimits(doc, purchase, Later));
            doc = WishlistService.AssignFunds(doc, Budget.Id, null, Budget.Id, Goal.Id, A.Id, 50_000, Bought);
            Equal(Bought, WishlistPresentation.DateLimits(doc, purchase, Later).Latest);
            var before = JsonSerializer.Serialize(doc);
            Reject(() => WishlistService.EditPurchaseDate(doc, purchase.Id, Later));
            Equal(before, JsonSerializer.Serialize(doc));
            var moved = WishlistService.EditPurchaseDate(doc, purchase.Id, Funded);
            foreach (var manual in doc.WishlistEvents.TakeLast(2)) Equal(manual, moved.WishlistEvents.Single(e => e.Id == manual.Id));
        });
        test("shared date correction keeps historical fee validation and rejects direct internal edits", () => {
            var doc = Fixture(feeNeedsIncome: true); var before = JsonSerializer.Serialize(doc);
            Reject(() => WishlistService.EditPurchaseDate(doc, Purchase(doc).Id, Funded));
            foreach (var activity in doc.WishlistEvents.Where(e => e.InternalFunding))
                Reject(() => WishlistService.EditActivityDate(doc, activity.Id, Later));
            Equal(before, JsonSerializer.Serialize(doc));
        });
        test("fully spent shared allocation still allows a purchase date correction", () => {
            var source = Fixture(500_000, 0); var purchase = Purchase(source);
            var changed = WishlistService.EditActivityDate(source, purchase.Id, Later);
            Equal(source.WishlistEvents.Count, changed.WishlistEvents.Count);
            Equal(Later, changed.Transactions.Single().Date);
        });
        test("format twelve surplus remains linked through save reopen and repeated date corrections", () => {
            var folder = Path.Combine(Path.GetTempPath(), "Piggy-date-group-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var file = Path.Combine(folder, "fictional.mpb"); var session = new DocumentSession();
                session.Apply(Fixture()); session.Save(file); session.Open(file);
                var purchase = Purchase(session.Document);
                session.Apply(WishlistService.EditActivityDate(session.Document, purchase.Id, Later));
                session.Save(file); session.Open(file);
                Equal(12, session.Document.Version);
                var changed = WishlistService.EditPurchaseDate(session.Document, purchase.Id, Funded);
                Equal(Funded, changed.Transactions.Single().Date);
                var reversed = WishlistService.CorrectPurchase(changed, Budget.Id, Later, purchase.Id);
                Equal(0, reversed.Transactions.Count);
                Equal(1_000_000m, reversed.Balance(A.Id));
                Equal(700_000m, reversed.WishlistAllocated(Budget.Id));
            }
            finally { Directory.Delete(folder, true); }
        });
    }
}

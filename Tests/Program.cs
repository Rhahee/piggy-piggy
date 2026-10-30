using MyPiggyBank.Core;
using System.Text.Json;

var passed = 0;
void Test(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected,actual)) throw new Exception($"Expected {expected}, got {actual}"); }
void Reject(Action action) { try { action(); } catch (Exception e) when (e is InvalidDataException or JsonException or IOException or UnauthorizedAccessException) { return; } throw new Exception("Expected rejection"); }
var start = new DateOnly(2026,10,1);
var bankA = new Bank(Guid.NewGuid(),"BCA",start,3_000_000);
var bankB = new Bank(Guid.NewGuid(),"Mandiri",start,2_000_000);
var salary = new Category(Guid.NewGuid(),"Salary",CategoryKind.Income);
var food = new Category(Guid.NewGuid(),"Groceries",CategoryKind.Expense);
var fees = new Category(Guid.NewGuid(),"Bank Fees",CategoryKind.Expense);
FinanceDocument Empty() => new() { Banks = [bankA,bankB], Categories = [salary,food,fees] };
Transaction Income(decimal amount = 8_000_000) => new(Guid.NewGuid(),new(2026,10,23),TransactionKind.Income,bankA.Id,null,salary.Id,amount,0,null,"October salary");
Transaction Expense() => new(Guid.NewGuid(),new(2026,10,25),TransactionKind.Expense,bankA.Id,null,food.Id,250_000,0,null,"Weekly groceries");
Transaction Transfer(decimal fee = 2500) => new(Guid.NewGuid(),new(2026,10,24),TransactionKind.Transfer,bankA.Id,bankB.Id,null,1_000_000,fee,null,"Savings transfer");
FinanceDocument Complete(decimal fee = 2500) { var d = Empty(); d.Transactions = [Income(),Transfer(fee),Expense()]; return d; }
Test("empty document and no default categories",() => { var d = new FinanceDocument(); d.Validate(); Equal(0,d.Categories.Count); });
Test("income and expense affect source only",() => { var d = Empty(); d.Transactions = [Income(),Expense()]; d.Validate(); Equal(10_750_000m,d.Balance(bankA.Id)); Equal(2_000_000m,d.Balance(bankB.Id)); });
Test("transfer reduces source including fee",() => { var d = Complete(); d.Validate(); Equal(9_747_500m,d.Balance(bankA.Id)); Equal(3_000_000m,d.Balance(bankB.Id)); });
Test("transfer preserves total except fee",() => { var d = Empty(); d.Transactions = [Transfer()]; Equal(4_997_500m,d.Banks.Sum(b => d.Balance(b.Id))); });
Test("monthly totals exclude transfer principal and opening balances",() => { var totals = Complete().Totals(start,new(2026,10,31)); Equal(8_000_000m,totals.Income); Equal(252_500m,totals.Expense); });
Test("bank totals charge fee only to source",() => { var d = Complete(); Equal(252_500m,d.Totals(start,new(2026,10,31),bankA.Id).Expense); Equal(0m,d.Totals(start,new(2026,10,31),bankB.Id).Expense); });
Test("backdated entries update subsequent balances",() => { var d = Complete(); var before = d.Balance(bankA.Id,new(2026,10,24)); d.Transactions.Add(Expense() with { Date = new(2026,10,22) }); Equal(before - 250_000,d.Balance(bankA.Id,new(2026,10,24))); });
Test("balance before opening date is zero",() => Equal(0m,Complete().Balance(bankA.Id,start.AddDays(-1))));
Test("negative history allowed and detected despite positive final balance",() => { var d = Empty(); d.Transactions = [Expense() with { Date = start, Amount = 4_000_000 },Income()]; d.Validate(); Equal(true,d.HasNegativeHistory(bankA.Id)); Equal(true,d.Balance(bankA.Id) > 0); });
Test("same-day records treated as date aggregate",() => { var d = Empty(); d.Transactions = [Expense() with { Date = start,Amount = 4_000_000 }, Income() with { Date = start }]; Equal(false,d.HasNegativeHistory(bankA.Id)); });
Test("edit transfer updates both balances and linked fee",() => { var d = Complete(); d.Transactions[1] = Transfer() with { Amount = 500_000, Fee = 1000 }; Equal(10_249_000m,d.Balance(bankA.Id)); Equal(2_500_000m,d.Balance(bankB.Id)); Equal(251_000m,d.Totals(start,new(2026,10,31)).Expense); });
Test("delete transfer removes both legs and fee",() => { var d = Complete(); d.Transactions.RemoveAll(t => t.Kind == TransactionKind.Transfer); Equal(10_750_000m,d.Balance(bankA.Id)); Equal(2_000_000m,d.Balance(bankB.Id)); Equal(250_000m,d.Totals(start,new(2026,10,31)).Expense); });
void Invalid(string name, Action<FinanceDocument> mutate) => Test(name,() => { var d = Complete(); mutate(d); Reject(d.Validate); });
Invalid("duplicate banks ignore case",d => d.Banks.Add(bankB with { Id = Guid.NewGuid(),Name = "bca" }));
Invalid("bank names must be trimmed",d => d.Banks[0] = bankA with { Name = " BCA " });
Invalid("missing category rejected",d => d.Transactions[0] = Income() with { CategoryId = null });
Invalid("wrong category type rejected",d => d.Transactions[0] = Income() with { CategoryId = food.Id });
Invalid("system fee cannot be assigned a user category",d => d.Transactions[1] = Transfer() with { FeeCategoryId = salary.Id });
Invalid("transfer cannot target same bank",d => d.Transactions[1] = Transfer() with { ToBankId = bankA.Id });
Invalid("source opening date enforced",d => d.Transactions[0] = Income() with { Date = start.AddDays(-1) });
Invalid("destination opening date enforced",d => d.Banks[1] = bankB with { OpeningDate = new(2026,10,25) });
Invalid("opening date change cannot invalidate history",d => d.Banks[0] = bankA with { OpeningDate = new(2026,11,1) });
Invalid("zero amount rejected",d => d.Transactions[0] = Income(0));
Invalid("negative amount rejected",d => d.Transactions[0] = Income(-1));
Invalid("fraction beyond two decimals rejected",d => d.Transactions[0] = Income(0.001m));
Invalid("negative fee rejected",d => d.Transactions[1] = Transfer() with { Fee = -1 });
Invalid("zero fee cannot have linked category",d => d.Transactions[1] = Transfer(0) with { FeeCategoryId = fees.Id });
Invalid("income cannot have destination",d => d.Transactions[0] = Income() with { ToBankId = bankB.Id });
Invalid("unknown bank rejected",d => d.Transactions[0] = Income() with { BankId = Guid.NewGuid() });
Invalid("unknown enum rejected",d => d.Transactions[0] = Income() with { Kind = (TransactionKind)100 });
Invalid("duplicate transaction ID rejected",d => d.Transactions.Add(d.Transactions[0]));
Invalid("unsupported version rejected",d => d.Version = 10);
Invalid("wrong format marker rejected",d => d.Format = "OtherApp");
Test("optional description may be blank",() => { var d = Complete(); d.Transactions[0] = Income() with { Description = "" }; d.Validate(); });
Test("category names can be reused across different types",() => { var d = Empty(); d.Categories.Add(new(Guid.NewGuid(),"Salary",CategoryKind.Expense)); d.Validate(); });
Test("money formatting exact",() => { Equal("Rp 1,250,000.00",Display.Money(1250000)); Equal("Rp 0.00",Display.Money(0)); Equal("−Rp 2.50",Display.Money(-2.5m)); });
Test("money parsing grouped and ungrouped",() => { Equal(1250000.50m,Display.ParseMoney("1,250,000.50")); Equal(1250000m,Display.ParseMoney("1250000")); });
Test("ambiguous separators and excessive decimals rejected",() => { foreach (var v in new[] { "1.250.000,00","1,25","1.001","1e6","NaN","","1,,000" }) Reject(() => Display.ParseMoney(v)); });
Test("date format exact",() => Equal("10/05/2026",Display.Date(new(2026,10,5))));
Test("empty money field stays empty; only optional zero accepts blank",() => { Equal("",MoneyEditing.Format("",0).Text); Reject(() => MoneyEditing.Read("")); Equal(0m,MoneyEditing.Read("",emptyAsZero:true)); });
Test("typing 100000 inserts grouping without an editable currency prefix",() => { var state = new MoneyEdit("",0); foreach (var digit in "100000") state = MoneyEditing.Format(state.Text.Insert(state.Caret,digit.ToString()),state.Caret + 1); Equal("100,000",state.Text); Equal(7,state.Caret); Equal(100000m,MoneyEditing.Read(state.Text)); });
Test("decimal digits remain editable and are not rounded while typing",() => { var state = new MoneyEdit("",0); foreach (var digit in "1234.50") state = MoneyEditing.Format(state.Text.Insert(state.Caret,digit.ToString()),state.Caret + 1); Equal("1,234.50",state.Text); Equal(1234.50m,MoneyEditing.Read(state.Text)); Equal("0.01",MoneyEditing.Format("0.01",4).Text); });
Test("caret follows a middle insertion across regrouping",() => { var state = MoneyEditing.Format("129,345",3); Equal("129,345",state.Text); Equal(3,state.Caret); var regrouped = MoneyEditing.Format("12345",2); Equal("12,345",regrouped.Text); Equal(2,regrouped.Caret); });
Test("deletion regrouping and transient negative input preserve editing",() => { Equal("100",MoneyEditing.Format("1,00",4).Text); Equal("-",MoneyEditing.Format("-",1).Text); Equal("-1,250.50",MoneyEditing.Format("-1250.50",8).Text); Equal("0.5",MoneyEditing.Format(".5",2).Text); });
Test("pasting formatted Rupiah becomes numeric input",() => { Equal("1,250,000.50",MoneyEditing.Paste("Rp 1,250,000.50")); Equal("100,000",MoneyEditing.Paste("100000")); Equal("-2,500.00",MoneyEditing.Paste("−Rp 2,500.00")); });
Test("ambiguous clipboard separators do not silently change the amount",() => { foreach (var value in new[] { "1,25","1,,000","1.250.000,00","100000Rp 0.00","12abc" }) Reject(() => MoneyEditing.Paste(value)); });
Test("editor enforces two decimals and supported integer range",() => { Reject(() => MoneyEditing.Format("1.234",5)); Reject(() => MoneyEditing.Format("1000000000000000",16)); Equal("999,999,999,999,999.99",MoneyEditing.Format("999999999999999.99",18).Text); });
Test("unfinished decimal separator commits without changing value",() => { Equal("1,000.",MoneyEditing.Format("1000.",5).Text); Equal(1000m,MoneyEditing.Read("1,000.")); Equal("100,000.00",MoneyEditing.Read("100,000").ToString("N2",Display.Culture)); });
Test("transfer fee works with no user-created categories",() => { var d = Empty(); d.Categories.Clear(); d.Transactions = [Transfer()]; d.Validate(); Equal(0,d.Categories.Count); Equal(2500m,d.Totals(start,new(2026,10,31)).Expense); Equal(1_997_500m,d.Balance(bankA.Id)); });
Test("zero-fee transfer does not create any system category entry",() => { var s = new DocumentSession(); var d = Empty(); d.Categories.Clear(); d.Transactions = [Transfer(0)]; s.Apply(d); Equal(0,s.Document.Categories.Count); Equal(0m,s.Document.Totals(start,new(2026,10,31)).Expense); });
Test("fixed Admin Fee filter label needs no mutable category",() => { var d = new FinanceDocument(); Equal("Admin Fee",d.CategoryName(FinanceDocument.AdminFeeFilterId)); Equal(0,d.Categories.Count); });
var savings = new Purpose(Guid.NewGuid(),"Tabungan"); var debt = new Purpose(Guid.NewGuid(),"Hutang");
FinanceDocument Allocated()
{
    var d = Complete(); d.Purposes = [savings,debt];
    d.Transactions[1] = Transfer() with { Allocations = new[] { new TransferAllocation(savings.Id,800_000),new TransferAllocation(debt.Id,200_000) } };
    return d;
}
Test("allocations do not change balances or income and expense totals",() => { var d = Allocated(); d.Validate(); var plain = Complete(); Equal(plain.Balance(bankA.Id),d.Balance(bankA.Id)); Equal(plain.Balance(bankB.Id),d.Balance(bankB.Id)); Equal(plain.Totals(start,new(2026,10,31)),d.Totals(start,new(2026,10,31))); });
void InvalidAllocation(string name,Action<FinanceDocument> change) => Test(name,() => { var d = Allocated(); change(d); Reject(d.Validate); });
InvalidAllocation("allocation total cannot be below transfer amount",d => d.Transactions[1] = d.Transactions[1] with { Allocations = new[] { new TransferAllocation(savings.Id,800_000) } });
InvalidAllocation("allocation total excludes admin fee",d => d.Transactions[1] = d.Transactions[1] with { Allocations = new[] { new TransferAllocation(savings.Id,1_002_500) } });
InvalidAllocation("allocation must reference an existing purpose",d => d.Transactions[1] = d.Transactions[1] with { Allocations = new[] { new TransferAllocation(Guid.NewGuid(),1_000_000) } });
Test("allocation amounts must be positive with at most two decimals",() => { foreach (var amount in new[] { 0m,-1m,0.001m }) { var d = Allocated(); d.Transactions[1] = d.Transactions[1] with { Allocations = new[] { new TransferAllocation(savings.Id,amount) } }; Reject(d.Validate); } });
InvalidAllocation("income and expense cannot carry transfer allocations",d => d.Transactions[0] = d.Transactions[0] with { Allocations = new[] { new TransferAllocation(savings.Id,8_000_000) } });
InvalidAllocation("purpose cannot repeat within one transfer",d => d.Transactions[1] = d.Transactions[1] with { Allocations = new[] { new TransferAllocation(savings.Id,800_000),new TransferAllocation(savings.Id,200_000) } });
InvalidAllocation("purpose names are unique ignoring case",d => d.Purposes.Add(new(Guid.NewGuid(),"tabungan")));
Test("purpose names must be trimmed nonempty and limited",() => { foreach (var name in new[] { ""," "," Tabungan",new string('x',101) }) { var d = new FinanceDocument { Purposes = [new(Guid.NewGuid(),name)] }; Reject(d.Validate); } });
InvalidAllocation("referenced purpose cannot be removed",d => d.Purposes.RemoveAll(p => p.Id == savings.Id));
Test("purpose rename preserves references and financial values",() => { var d = Allocated(); d.Purposes[0] = savings with { Name = "Emergency Fund" }; d.Validate(); Equal(true,d.AllocationSummary(d.Transactions[1]).Contains("Emergency Fund")); Equal(9_747_500m,d.Balance(bankA.Id)); });
Test("purpose catalog is independent of income and expense categories",() => { var d = Allocated(); d.Categories.Add(new(Guid.NewGuid(),"Tabungan",CategoryKind.Expense)); d.Validate(); Equal("Tabungan",d.PurposeName(savings.Id)); Equal(0,new FinanceDocument().Purposes.Count); });
Test("session isolates editable allocation arrays from its caller",() => { var d = Allocated(); var rows = (TransferAllocation[])d.Transactions[1].Allocations!; var s = new DocumentSession(); s.Apply(d); rows[0] = new(savings.Id,1); Equal(800_000m,s.Document.Transactions[1].Allocations![0].Amount); });
InvalidAllocation("older document versions cannot silently carry allocations",d => d.Version = 2);
Test("expense admin fee reduces the source and counts once in expenses",() => { var d = Empty(); d.Transactions = [Expense() with { Amount = 100_000, Fee = 2500 }]; d.Validate(); Equal(2_897_500m,d.Balance(bankA.Id)); Equal(102_500m,d.Totals(start,new(2026,10,31)).Expense); Equal(2_000_000m,d.Balance(bankB.Id)); });
Test("income before fee records gross income and credits net",() => { var d = Empty(); d.Transactions = [Income(100_000) with { Fee = 2500 }]; d.Validate(); Equal(3_097_500m,d.Balance(bankA.Id)); var totals = d.Totals(start,new(2026,10,31)); Equal(100_000m,totals.Income); Equal(2500m,totals.Expense); });
Test("income after fee reconstructs gross without deducting fee twice",() => { var d = Empty(); d.Transactions = [Income(97_500) with { Fee = 2500, IncomeBasis = IncomeAmountBasis.AfterFee }]; d.Validate(); Equal(3_097_500m,d.Balance(bankA.Id)); var totals = d.Totals(start,new(2026,10,31)); Equal(100_000m,totals.Income); Equal(2500m,totals.Expense); Equal(97_500m,totals.Income-totals.Expense); });
Test("both income bases agree when fee is zero",() => { var d = Empty(); d.Transactions = [Income(100_000) with { IncomeBasis = IncomeAmountBasis.AfterFee }]; d.Validate(); Equal(3_100_000m,d.Balance(bankA.Id)); Equal(100_000m,d.Totals(start,new(2026,10,31)).Income); });
Test("editing fee or basis recalculates balance and deletion removes linked effect",() => { var d = Empty(); d.Transactions = [Income(100_000) with { Fee = 2500 }]; d.Transactions[0] = d.Transactions[0] with { IncomeBasis = IncomeAmountBasis.AfterFee }; d.Validate(); Equal(3_100_000m,d.Balance(bankA.Id)); d.Transactions[0] = d.Transactions[0] with { Fee = 5000, IncomeBasis = IncomeAmountBasis.BeforeFee }; Equal(3_095_000m,d.Balance(bankA.Id)); d.Transactions.Clear(); Equal(3_000_000m,d.Balance(bankA.Id)); });
Invalid("income before fee cannot have negative net",d => d.Transactions[0] = Income(100) with { Fee = 101 });
Invalid("income gross reconstruction cannot exceed supported money range",d => d.Transactions[0] = Income(999_999_999_999_999.99m) with { Fee = 1, IncomeBasis = IncomeAmountBasis.AfterFee });
Invalid("income fee must not be negative",d => d.Transactions[0] = Income() with { Fee = -1 });
Invalid("expense fee must not have excess decimals",d => d.Transactions[2] = Expense() with { Fee = 0.001m });
Invalid("unknown income basis rejected",d => d.Transactions[0] = Income() with { IncomeBasis = (IncomeAmountBasis)5 });
Invalid("expense cannot carry income basis",d => d.Transactions[2] = Expense() with { IncomeBasis = IncomeAmountBasis.AfterFee });
Invalid("old format cannot silently carry income or expense fees",d => { d.Version = 3; d.Transactions[2] = Expense() with { Fee = 100 }; });
Test("fees participate in negative history and bank monthly totals",() => { var d = Empty(); d.Transactions = [Expense() with { Amount = 2_999_000, Fee = 2000 }]; d.Validate(); Equal(true,d.HasNegativeHistory(bankA.Id)); Equal(0m,d.Totals(start,new(2026,10,31),bankB.Id).Expense); Equal(3_001_000m,d.Totals(start,new(2026,10,31),bankA.Id).Expense); });
string ReleaseJson(string tag, bool draft = false, bool prerelease = false, string? assetUrl = null, string? assetName = null, long size = 1000)
{
    var version = tag.TrimStart('v'); var name = assetName ?? $"Piggy-Piggy-{version}-win-x64.zip";
    return JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease, body = "Release notes", assets = new[] { new { name, size, browser_download_url = assetUrl ?? $"https://github.com/Rhahee/piggy-piggy/releases/download/{tag}/{name}" } } });
}
Test("published same version is up to date despite assembly revision zero",() => Equal(UpdateStatus.UpToDate,ReleaseChecker.Parse(ReleaseJson("v1.1.1"),new Version(1,1,1,0)).Status));
Test("older release never offers a downgrade",() => Equal(UpdateStatus.UpToDate,ReleaseChecker.Parse(ReleaseJson("v1.0.9"),new Version(1,1,1)).Status));
Test("newer portable release exposes expected version and repository links",() => { var r = ReleaseChecker.Parse(ReleaseJson("v1.2.0"),new Version(1,1,1)); Equal(UpdateStatus.Available,r.Status); Equal(new Version(1,2,0),r.Release!.Version); Equal("https://github.com/Rhahee/piggy-piggy/releases/tag/v1.2.0",r.Release.Page.AbsoluteUri); Equal(true,r.Release.Download.AbsoluteUri.EndsWith("Piggy-Piggy-1.2.0-win-x64.zip")); });
Test("version comparison is numeric rather than alphabetical",() => Equal(UpdateStatus.Available,ReleaseChecker.Parse(ReleaseJson("v1.10.0"),new Version(1,9,0)).Status));
Test("draft release cannot produce a current or available status",() => Reject(() => ReleaseChecker.Parse(ReleaseJson("v1.2.0",draft:true),new Version(1,1,1))));
Test("prerelease is not offered",() => Reject(() => ReleaseChecker.Parse(ReleaseJson("v1.2.0",prerelease:true),new Version(1,1,1))));
Test("invalid release version is rejected",() => Reject(() => ReleaseChecker.Parse(ReleaseJson("latest"),new Version(1,1,1))));
Test("version suffix is not treated as stable",() => Reject(() => ReleaseChecker.Parse(ReleaseJson("v1.2.0-beta"),new Version(1,1,1))));
Test("newer release missing compatible ZIP stays neutral",() => Equal(UpdateStatus.Unavailable,ReleaseChecker.Parse(ReleaseJson("v1.2.0",assetName:"source.zip"),new Version(1,1,1)).Status));
Test("download links outside this repository are rejected",() => Equal(UpdateStatus.Unavailable,ReleaseChecker.Parse(ReleaseJson("v1.2.0",assetUrl:"https://example.com/update.zip"),new Version(1,1,1)).Status));
Test("empty ZIP is not offered",() => Equal(UpdateStatus.Unavailable,ReleaseChecker.Parse(ReleaseJson("v1.2.0",size:0),new Version(1,1,1)).Status));
Test("release notes are bounded",() => { var json = ReleaseJson("v1.2.0").Replace("Release notes",new string('a',10_000)); Equal(true,ReleaseChecker.Parse(json,new Version(1,1,1)).Release!.Notes.Length < 8100); });
UpdateCheck HttpUpdate(Func<System.Net.Http.HttpRequestMessage,System.Net.Http.HttpResponseMessage> respond)
{
    using var client = new System.Net.Http.HttpClient(new UpdateHttpStub(respond));
    return new ReleaseChecker(client).CheckAsync(new Version(1,1,1)).GetAwaiter().GetResult();
}
Test("404 gives no published release rather than green",() => Equal(UpdateStatus.NoRelease,HttpUpdate(_ => new(System.Net.HttpStatusCode.NotFound)).Status));
Test("rate limit stays neutral",() => Equal(UpdateStatus.Unavailable,HttpUpdate(_ => new(System.Net.HttpStatusCode.Forbidden)).Status));
Test("network failure stays neutral",() => Equal(UpdateStatus.Unavailable,HttpUpdate(_ => throw new System.Net.Http.HttpRequestException("offline")).Status));
Test("malformed response stays neutral",() => Equal(UpdateStatus.Unavailable,HttpUpdate(_ => new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent("not json") }).Status));
Test("update request contains no document content or authentication",() => { var r = HttpUpdate(request => { Equal(ReleaseChecker.Endpoint,request.RequestUri!); Equal(System.Net.Http.HttpMethod.Get,request.Method); Equal(true,request.Headers.UserAgent.ToString().Contains("PiggyPiggy/1.1.1.0")); Equal(true,request.Headers.Authorization is null); Equal(true,request.Content is null); return new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(ReleaseJson("v1.2.0")) }; }); Equal(UpdateStatus.Available,r.Status); });
DateOnly Nov2026 = new(2026,11,1);
PaySchedule Schedule(int day, WeekendAdjustment weekend, DateOnly? first = null) => new(new[] { new PayRule(Nov2026,day,weekend,first ?? PaySchedule.ScheduledPayday(Nov2026,day,weekend)) });
Test("pay cycle keeps Saturday payday when configured",() => { var s = Schedule(21,WeekendAdjustment.KeepDate); s.Validate(); Equal(new DateOnly(2026,11,21),s.Cycle(Nov2026).Start); Equal(new DateOnly(2026,12,20),s.Cycle(Nov2026).End); });
Test("Saturday payday advances to Friday",() => { var s = Schedule(21,WeekendAdjustment.PreviousWorkingDay); s.Validate(); Equal(new DateOnly(2026,11,20),s.Cycle(Nov2026).Start); Equal(new DateOnly(2026,12,20),s.Cycle(Nov2026).End); });
Test("Saturday payday postpones to Monday",() => { var s = Schedule(21,WeekendAdjustment.NextWorkingDay); s.Validate(); Equal(new DateOnly(2026,11,23),s.Cycle(Nov2026).Start); Equal(new DateOnly(2026,12,20),s.Cycle(Nov2026).End); });
Test("Sunday adjustment uses Friday or Monday",() => { var m = new DateOnly(2026,6,1); Equal(new DateOnly(2026,6,19),PaySchedule.ScheduledPayday(m,21,WeekendAdjustment.PreviousWorkingDay)); Equal(new DateOnly(2026,6,22),PaySchedule.ScheduledPayday(m,21,WeekendAdjustment.NextWorkingDay)); });
Test("payday 31 clamps before applying weekend adjustment",() => { Equal(new DateOnly(2026,2,27),PaySchedule.ScheduledPayday(new(2026,2,1),31,WeekendAdjustment.PreviousWorkingDay)); Equal(new DateOnly(2026,3,2),PaySchedule.ScheduledPayday(new(2026,2,1),31,WeekendAdjustment.NextWorkingDay)); Equal(new DateOnly(2024,2,29),PaySchedule.ScheduledPayday(new(2024,2,1),31,WeekendAdjustment.KeepDate)); });
Test("shifted payday does not drift next month's nominal day",() => { var s = Schedule(21,WeekendAdjustment.NextWorkingDay); Equal(new DateOnly(2026,11,23),s.ActualPayday(Nov2026)); Equal(new DateOnly(2026,12,21),s.ActualPayday(new(2026,12,1))); });
Test("payday can shift across a month boundary",() => { var m = new DateOnly(2026,5,1); var s = new PaySchedule(new[] { new PayRule(m,31,WeekendAdjustment.NextWorkingDay,new(2026,6,1)) }); s.Validate(); Equal(new DateOnly(2026,6,29),s.Cycle(m).End); Equal(new DateOnly(2026,6,30),s.Cycle(m.AddMonths(1)).Start); });
Test("manual first payday defines first period start",() => { var s = Schedule(21,WeekendAdjustment.KeepDate,new(2026,11,24)); s.Validate(); Equal(new DateOnly(2026,11,24),s.Cycle(Nov2026).Start); Equal(new DateOnly(2026,12,21),s.ActualPayday(new(2026,12,1))); });
Test("adjacent pay cycles share no dates and leave no gaps",() => { var s = Schedule(21,WeekendAdjustment.NextWorkingDay); for(var i = 0;i < 24;i++) { var m = Nov2026.AddMonths(i); Equal(s.Cycle(m).End.AddDays(1),s.Cycle(m.AddMonths(1)).Start); } });
Test("new rules retain earlier paydays and affect selected month onward",() => { var s = new PaySchedule(new[] { new PayRule(Nov2026,21,WeekendAdjustment.KeepDate,new(2026,11,21)),new PayRule(new(2027,1,1),25,WeekendAdjustment.PreviousWorkingDay,new(2027,1,25)) }); s.Validate(); Equal(new DateOnly(2026,12,21),s.ActualPayday(new(2026,12,1))); Equal(new DateOnly(2027,1,24),s.Cycle(new(2026,12,1)).End); Equal(new DateOnly(2027,2,25),s.ActualPayday(new(2027,2,1))); });
Test("manual payday correction changes only that month's start",() => { var s = new PaySchedule(new[] { new PayRule(Nov2026,21,WeekendAdjustment.KeepDate,new(2026,11,21)),new PayRule(new(2026,12,1),21,WeekendAdjustment.KeepDate,new(2026,12,22)) }); s.Validate(); Equal(new DateOnly(2026,12,21),s.Cycle(Nov2026).End); Equal(new DateOnly(2027,1,21),s.ActualPayday(new(2027,1,1))); });
Test("non-increasing payday boundaries are rejected",() => Reject(() => Schedule(21,WeekendAdjustment.KeepDate,new(2026,12,22)).Validate()));
Test("future rule cannot overlap preceding cycle",() => { var s = new PaySchedule(new[] { new PayRule(Nov2026,21,WeekendAdjustment.KeepDate,new(2026,11,21)),new PayRule(new(2026,12,1),21,WeekendAdjustment.KeepDate,new(2026,11,20)) }); Reject(s.Validate); });
Test("invalid and duplicate schedule rules rejected",() => { Reject(() => Schedule(0,WeekendAdjustment.KeepDate,new(2026,11,21)).Validate()); Reject(() => new PaySchedule(Array.Empty<PayRule>()).Validate()); var s = Schedule(21,WeekendAdjustment.KeepDate); Reject(() => new PaySchedule(new[] { s.Rules[0],s.Rules[0] }).Validate()); });
Test("cycle before configured first month is rejected",() => Reject(() => Schedule(21,WeekendAdjustment.KeepDate).Cycle(new(2026,10,1))));
Test("calendar period retains full leap month",() => Equal(new DateOnly(2024,2,29),new ReportPeriod(ReportPeriodMode.CalendarMonth,new(2024,2,1),default,default).Range(null).End));
Test("custom range is inclusive and rejects reversed dates",() => { var p = new ReportPeriod(ReportPeriodMode.CustomRange,Nov2026,new(2026,11,20),new(2026,12,20)); var r = p.Range(null); Equal(true,r.Contains(new(2026,11,20))); Equal(true,r.Contains(new(2026,12,20))); Equal(false,r.Contains(new(2026,12,21))); Reject(() => (p with { CustomEnd = new(2026,11,19) }).Range(null)); });
Test("pay-cycle totals include boundary fees exactly once",() => { var d = Empty(); d.Transactions = [Income(100_000) with { Date = new(2026,11,20),Fee = 2500 },Expense() with { Date = new(2026,12,20),Amount = 50_000,Fee = 1000 },Income(999) with { Date = new(2026,12,21) }]; var r = Schedule(21,WeekendAdjustment.PreviousWorkingDay).Cycle(Nov2026); var t = d.Totals(r.Start,r.End); Equal(100_000m,t.Income); Equal(53_500m,t.Expense); });
Test("pay schedule is isolated by document copy",() => { var rules = new[] { new PayRule(Nov2026,21,WeekendAdjustment.KeepDate,new(2026,11,21)) }; var d = Empty(); d.PaySchedule = new(rules); var copy = d.Copy(); rules[0] = rules[0] with { MonthlyDay = 10 }; Equal(21,copy.PaySchedule!.Rules[0].MonthlyDay); });
Test("pay schedule does not affect financial balances",() => { var d = Complete(); var balance = d.Balance(bankA.Id); d.PaySchedule = Schedule(21,WeekendAdjustment.KeepDate); d.Validate(); Equal(balance,d.Balance(bankA.Id)); });
Invalid("old formats cannot silently contain pay settings",d => { d.Version = 4; d.PaySchedule = Schedule(21,WeekendAdjustment.KeepDate); });
var paySelection = new ReportPeriod(ReportPeriodMode.PayCycle,Nov2026,new(2026,11,1),new(2026,11,30));
Test("saved pay cycle restores instead of calendar month",() => {
    var d = Empty(); d.PaySchedule = Schedule(21,WeekendAdjustment.PreviousWorkingDay); d.SavedPeriod = paySelection; d.Validate();
    Equal(paySelection,ReportPeriod.Restore(d,new(2027,3,1))); Equal(paySelection,d.Copy().SavedPeriod!);
});
Test("legacy pay schedule restores pay cycle without requiring reconfiguration",() => {
    var d = Empty(); d.PaySchedule = Schedule(21,WeekendAdjustment.PreviousWorkingDay);
    var restored = ReportPeriod.Restore(d,new(2026,12,1)); Equal(ReportPeriodMode.PayCycle,restored.Mode);
    Equal(Nov2026,restored.Month); Equal(true,restored.Range(d.PaySchedule).Contains(new(2026,12,1)));
    Equal(true,d.SavedPeriod is null);
});
Test("dates before first rule retain pay cycle at first configured period",() => {
    var d = Empty(); d.PaySchedule = Schedule(21,WeekendAdjustment.KeepDate);
    var restored = ReportPeriod.Restore(d,new(2020,1,1)); Equal(ReportPeriodMode.PayCycle,restored.Mode); Equal(Nov2026,restored.Month); restored.Range(d.PaySchedule);
});
Test("explicit calendar choice overrides legacy pay cycle default",() => {
    var d = Empty(); d.PaySchedule = Schedule(21,WeekendAdjustment.KeepDate); d.SavedPeriod = paySelection with { Mode = ReportPeriodMode.CalendarMonth };
    Equal(ReportPeriodMode.CalendarMonth,ReportPeriod.Restore(d,new(2027,1,1)).Mode);
});
Test("new documents retain calendar default",() => Equal(ReportPeriodMode.CalendarMonth,ReportPeriod.Restore(Empty(),new(2026,10,1)).Mode));
Test("backdated or future transactions never reset pay cycle",() => {
    Equal(paySelection,paySelection.AfterTransaction(new(2020,1,1))); Equal(paySelection,paySelection.AfterTransaction(new(2030,1,1)));
});
Test("custom range remains fixed after an out of range transaction",() => {
    var custom = paySelection with { Mode = ReportPeriodMode.CustomRange }; Equal(custom,custom.AfterTransaction(new(2030,1,1)));
    var d = Empty(); d.SavedPeriod = custom; Equal(custom,ReportPeriod.Restore(d,new(2030,1,1)));
});
Test("calendar transaction navigation preserves calendar mode",() => {
    var moved = (paySelection with { Mode = ReportPeriodMode.CalendarMonth }).AfterTransaction(new(2027,1,15));
    Equal(ReportPeriodMode.CalendarMonth,moved.Mode); Equal(new DateOnly(2027,1,1),moved.Month);
});
Test("pay cycle lookup handles weekend shifts across nominal month boundaries",() => {
    var schedule = new PaySchedule(new[] { new PayRule(new(2026,10,1),1,WeekendAdjustment.PreviousWorkingDay,new(2026,10,1)) }); schedule.Validate();
    Equal(new DateOnly(2026,11,1),schedule.MonthContaining(new(2026,10,30))); Equal(new DateOnly(2026,10,1),schedule.MonthContaining(new(2026,10,29)));
});
Invalid("saved pay cycle requires a schedule",d => d.SavedPeriod = paySelection);
Invalid("invalid saved period mode rejected",d => d.SavedPeriod = paySelection with { Mode = (ReportPeriodMode)99 });
Invalid("saved custom range must be ordered",d => d.SavedPeriod = paySelection with { Mode = ReportPeriodMode.CustomRange, CustomStart = new(2026,12,1), CustomEnd = new(2026,11,1) });
Invalid("old formats cannot silently contain saved report preferences",d => { d.Version = 4; d.SavedPeriod = paySelection with { Mode = ReportPeriodMode.CalendarMonth }; });
Test("four-component release exposes the exact package name",() => {
    var result = ReleaseChecker.Parse(ReleaseJson("v1.2.4.1"),new Version(1,2,4,0)); Equal(UpdateStatus.Available,result.Status);
    Equal(new Version(1,2,4,1),result.Release!.Version); Equal(true,result.Release.Download.AbsoluteUri.EndsWith("Piggy-Piggy-1.2.4.1-win-x64.zip")); Equal(true,result.Message.Contains("1.2.4.1"));
});
Test("four-component revision comparison never offers a downgrade",() => Equal(UpdateStatus.UpToDate,ReleaseChecker.Parse(ReleaseJson("v1.2.4.1"),new Version(1,2,4,2)).Status));
Test("three-component bridge and revision zero are equivalent",() => {
    Equal(UpdateStatus.UpToDate,ReleaseChecker.Parse(ReleaseJson("v1.2.4"),new Version(1,2,4,0)).Status);
    Equal(UpdateStatus.UpToDate,ReleaseChecker.Parse(ReleaseJson("v1.2.4.0"),new Version(1,2,4)).Status);
    Equal(UpdateStatus.Available,ReleaseChecker.Parse(ReleaseJson("v1.2.4"),new Version(1,2,3,0)).Status);
});
Test("version display uses all four components",() => { Equal("1.2.4.0",AppVersion.Format(new Version(1,2,4))); Equal("1.2.4.5",AppVersion.Format(new Version(1,2,4,5))); });
Test("five-component release is rejected",() => Reject(() => ReleaseChecker.Parse(ReleaseJson("v1.2.4.0.1"),new Version(1,2,3))));

var updateFolder = Path.Combine(Path.GetTempPath(),"MyPiggyBankUpdateTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(updateFolder);
try
{
    var zipPath = Path.Combine(updateFolder,"package.zip");
    void Zip(params (string Name,string Text)[] entries)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = System.IO.Compression.ZipFile.Open(zipPath,System.IO.Compression.ZipArchiveMode.Create);
        foreach (var entry in entries) { using var writer = new StreamWriter(zip.CreateEntry(entry.Name).Open()); writer.Write(entry.Text); }
    }
    Test("checksum must match exact asset filename",() => {
        var hash = new string('a',64); Equal(hash,UpdatePackage.ReadChecksum(hash + "  package.zip\n","package.zip"));
        Reject(() => UpdatePackage.ReadChecksum(hash + "  another.zip","package.zip"));
        Reject(() => UpdatePackage.ReadChecksum("1234  package.zip","package.zip"));
    });
    Test("valid package extracts only the expected executable",() => {
        Zip(("Piggy - Piggy.exe","MZ-test"),("../escape.txt","unsafe"));
        var dir = Path.Combine(updateFolder,"valid"); Directory.CreateDirectory(dir);
        var staged = UpdatePackage.Extract(zipPath,UpdatePackage.Hash(zipPath),dir);
        Equal("MZ-test",File.ReadAllText(staged.Executable)); Equal(1,Directory.GetFiles(dir).Length);
    });
    Test("corrupt package never reaches replacement",() => {
        Zip(("Piggy - Piggy.exe","MZ-test"));
        Reject(() => UpdatePackage.Extract(zipPath,new string('0',64),updateFolder));
        Equal(false,File.Exists(Path.Combine(updateFolder,"update.exe")));
    });
    Test("duplicate executables are rejected",() => {
        Zip(("Piggy - Piggy.exe","MZ-a"),("Piggy - Piggy.exe","MZ-b"));
        Reject(() => UpdatePackage.Extract(zipPath,UpdatePackage.Hash(zipPath),updateFolder));
    });
    Test("nested paths are not treated as portable executable",() => {
        Zip(("nested/Piggy - Piggy.exe","MZ-a")); Reject(() => UpdatePackage.Extract(zipPath,UpdatePackage.Hash(zipPath),updateFolder));
    });
    Test("non executable content rejected",() => {
        Zip(("Piggy - Piggy.exe","not an exe")); var dir = Path.Combine(updateFolder,"invalid"); Directory.CreateDirectory(dir);
        Reject(() => UpdatePackage.Extract(zipPath,UpdatePackage.Hash(zipPath),dir));
    });
    var target = Path.Combine(updateFolder,"target.exe"); var payload = Path.Combine(updateFolder,"payload.exe");
    File.WriteAllText(payload,"new");
    Test("successful replacement retains old EXE backup and leaves documents untouched",() => {
        File.WriteAllText(target,"old"); var doc = Path.Combine(updateFolder,"my data.mpb"); File.WriteAllText(doc,"financial data");
        var launched = false; var backup = UpdatePackage.Replace(payload,target,UpdatePackage.Hash(payload),() => launched = true);
        Equal(true,launched); Equal("new",File.ReadAllText(target)); Equal("old",File.ReadAllText(backup)); Equal("financial data",File.ReadAllText(doc));
    });
    Test("restart launch failure restores old executable",() => {
        File.WriteAllText(target,"old"); Reject(() => UpdatePackage.Replace(payload,target,UpdatePackage.Hash(payload),() => throw new IOException("launch failed")));
        Equal("old",File.ReadAllText(target)); Equal(0,Directory.GetFiles(updateFolder,"*.new").Length);
    });
    Test("modified staged executable does not replace old app",() => {
        File.WriteAllText(target,"old"); Reject(() => UpdatePackage.Replace(payload,target,new string('0',64),() => throw new Exception("must not launch")));
        Equal("old",File.ReadAllText(target));
    });
    Test("locked executable fails without corrupting old app",() => {
        File.WriteAllText(target,"old"); using (var locked = new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))
            Reject(() => UpdatePackage.Replace(payload,target,UpdatePackage.Hash(payload),() => throw new Exception("must not launch")));
        Equal("old",File.ReadAllText(target));
    });
    Test("release checksum must come from the expected repository asset",() => {
        var json = ReleaseJson("v1.2.0");
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        node["assets"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject { ["name"] = "Piggy-Piggy-1.2.0-win-x64.zip.sha256", ["size"] = 120, ["browser_download_url"] = "https://github.com/Rhahee/piggy-piggy/releases/download/v1.2.0/Piggy-Piggy-1.2.0-win-x64.zip.sha256" });
        Equal(true,ReleaseChecker.Parse(node.ToJsonString(),new Version(1,1,1)).Release!.Checksum is not null);
        node["assets"]![1]!["browser_download_url"] = "https://example.com/hash";
        Equal(true,ReleaseChecker.Parse(node.ToJsonString(),new Version(1,1,1)).Release!.Checksum is null);
    });
}
finally { Directory.Delete(updateFolder,true); }
byte[] DownloadZip()
{
    using var buffer = new MemoryStream();
    using (var zip = new System.IO.Compression.ZipArchive(buffer,System.IO.Compression.ZipArchiveMode.Create,true))
    { using var writer = new StreamWriter(zip.CreateEntry("Piggy - Piggy.exe").Open()); writer.Write("MZ-test-download"); }
    return buffer.ToArray();
}
var downloadBytes = DownloadZip();
var downloadHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(downloadBytes));
var testDownload = new ReleaseInfo(new Version(1,2,2),new Uri("https://github.com/Rhahee/piggy-piggy/releases/tag/v1.2.2"),
    new Uri("https://github.com/Rhahee/piggy-piggy/releases/download/v1.2.2/Piggy-Piggy-1.2.2-win-x64.zip"),"",
    new Uri("https://github.com/Rhahee/piggy-piggy/releases/download/v1.2.2/Piggy-Piggy-1.2.2-win-x64.zip.sha256"));
StagedUpdate DownloadFixture(string? checksum = null, System.Net.HttpStatusCode status = System.Net.HttpStatusCode.OK, long? reportedLength = null, CancellationToken token = default)
{
    using var client = new System.Net.Http.HttpClient(new UpdateHttpStub(request => {
        Equal(true,request.Headers.Authorization is null); Equal(true,request.Content is null);
        if (request.RequestUri == testDownload.Checksum) return new(System.Net.HttpStatusCode.OK) { Content = new System.Net.Http.StringContent(checksum ?? (downloadHash + "  " + Path.GetFileName(testDownload.Download.LocalPath))) };
        Equal(testDownload.Download,request.RequestUri!);
        var response = new System.Net.Http.HttpResponseMessage(status) { Content = new System.Net.Http.ByteArrayContent(downloadBytes) };
        if (reportedLength.HasValue) response.Content.Headers.ContentLength = reportedLength.Value;
        return response;
    }));
    return new UpdateDownloader(client).DownloadAsync(testDownload,new Progress<string>(),token).GetAwaiter().GetResult();
}
Test("download verifies checksum and extracts package automatically",() => {
    var staged = DownloadFixture(); try { Equal("MZ-test-download",File.ReadAllText(staged.Executable)); Equal(UpdatePackage.Hash(staged.Executable),staged.Sha256); }
    finally { UpdatePackage.Cleanup(staged.Directory); } Equal(false,Directory.Exists(staged.Directory));
});
Test("download rejects mismatching checksum",() => Reject(() => DownloadFixture(new string('0',64) + "  " + Path.GetFileName(testDownload.Download.LocalPath))));
Test("download rejects oversized checksum response",() => Reject(() => DownloadFixture(new string('a',5000))));
Test("download rejects missing HTTP package",() => {
    try { DownloadFixture(status:System.Net.HttpStatusCode.NotFound); throw new Exception("Expected HTTP rejection"); }
    catch (System.Net.Http.HttpRequestException) { }
});
Test("download rejects excessive declared size",() => Reject(() => DownloadFixture(reportedLength:UpdatePackage.MaxPackageBytes + 1)));
Test("download rejects truncated response",() => Reject(() => DownloadFixture(reportedLength:downloadBytes.Length + 1)));
Test("cancelled download does not produce a staged update",() => {
    using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
    try { DownloadFixture(token:cancelled.Token); throw new Exception("Expected cancellation"); } catch (OperationCanceledException) { }
});
Test("missing release checksum never starts a download",() => {
    using var client = new System.Net.Http.HttpClient(new UpdateHttpStub(_ => throw new Exception("Must not make a request")));
    Reject(() => new UpdateDownloader(client).DownloadAsync(testDownload with { Checksum = null },new Progress<string>(),default).GetAwaiter().GetResult());
});

var wishlistId=Guid.NewGuid(); var wishlistDate=new DateOnly(2026,10,24);
WishlistItem Laptop() => new(wishlistId,"Laptop",5_000_000,new(2026,11,30),WishlistPriority.High,"Work and personal projects.");
FinanceDocument WishlistEmpty() => WishlistService.Create(new FinanceDocument { Banks=[bankA with {OpeningBalance=5_000_000},bankB with {OpeningBalance=5_000_000,Name="OCBC"}],Categories=[salary,food] },Laptop());
FinanceDocument WishlistFunded() { var d=WishlistEmpty(); d=WishlistService.Allocate(d,wishlistId,bankA.Id,1_000_000,wishlistDate);return WishlistService.Allocate(d,wishlistId,bankB.Id,4_000_000,wishlistDate); }
FinanceDocument WishlistMoved() => WishlistService.Move(WishlistFunded(),wishlistId,bankA.Id,bankB.Id,900_000,2500,wishlistDate,true);
FinanceDocument WishlistBought() => WishlistService.Purchase(WishlistMoved(),wishlistId,bankB.Id,food.Id,4_900_000,3000,wishlistDate);
Test("wishlist starts without allocation or an account",()=>{var d=WishlistEmpty();Equal(0m,d.WishlistAllocated(wishlistId));Equal(0,d.Transactions.Count);Equal(WishlistStatus.Planned,d.WishlistState(wishlistId));});
Test("allocation preserves bank balance and does not create income or expense",()=>{var d=WishlistFunded();Equal(5_000_000m,d.Balance(bankA.Id));Equal(5_000_000m,d.WishlistAllocated(wishlistId));Equal(4_000_000m,d.AvailableBalance(bankA.Id));Equal(0,d.Transactions.Count);});
Test("allocation cannot exceed available balance",()=>Reject(()=>WishlistService.Allocate(WishlistFunded(),wishlistId,bankB.Id,1_000_001,wishlistDate)));
Test("same money cannot fund two wishlists",()=>{var d=WishlistFunded();var next=Laptop() with {Id=Guid.NewGuid(),Name="Phone"};d=WishlistService.Create(d,next);Reject(()=>WishlistService.Allocate(d,next.Id,bankB.Id,1_500_000,wishlistDate));});
Test("ordinary expense cannot spend reserved wishlist funds",()=>{var d=WishlistFunded();d.Transactions.Add(Expense() with {Date=wishlistDate,Amount=4_500_000});Reject(d.Validate);});
Test("ordinary expense can exactly use available balance",()=>{var d=WishlistFunded();d.Transactions.Add(Expense() with {Date=wishlistDate,Amount=4_000_000});d.Validate();Equal(0m,d.AvailableBalance(bankA.Id));});
Test("ordinary admin fee cannot consume allocations",()=>{var d=WishlistFunded();d.Transactions.Add(Expense() with {Date=wishlistDate,Amount=4_000_000,Fee=1});Reject(d.Validate);});
Test("ordinary transfer cannot consume allocations",()=>{var d=WishlistFunded();d.Transactions.Add(Transfer(0) with {Date=wishlistDate,Amount=4_500_000});Reject(d.Validate);});
Test("opening balance edit is checked against allocated history",()=>{var d=WishlistFunded();d.Banks[0]=d.Banks[0] with {OpeningBalance=500_000};Reject(d.Validate);});
Test("historical transaction before allocations stays valid when funding remains sufficient",()=>{var d=WishlistFunded();d.Transactions.Add(Expense() with {Date=new(2026,10,10),Amount=1_000_000});d.Validate();Equal(3_000_000m,d.AvailableBalance(bankA.Id));});
Test("future transaction cannot break existing reservations",()=>{var d=WishlistFunded();d.Transactions.Add(Expense() with {Date=new(2027,1,1),Amount=4_500_000});Reject(d.Validate);});
Test("wishlist transfer moves allocation with principal and charges fee to free balance",()=>{var d=WishlistMoved();Equal(4_097_500m,d.Balance(bankA.Id));Equal(5_900_000m,d.Balance(bankB.Id));Equal(100_000m,d.WishlistAllocated(wishlistId,bankA.Id));Equal(4_900_000m,d.WishlistAllocated(wishlistId,bankB.Id));Equal(5_000_000m,d.WishlistAllocated(wishlistId));Equal(3_997_500m,d.AvailableBalance(bankA.Id));Equal(2500m,d.Totals(start,new(2026,10,31)).Expense);});
Test("reassignment changes only reservations",()=>{var d=WishlistService.Move(WishlistFunded(),wishlistId,bankA.Id,bankB.Id,500_000,0,wishlistDate,false);Equal(5_000_000m,d.Balance(bankA.Id));Equal(5_000_000m,d.Balance(bankB.Id));Equal(4_500_000m,d.WishlistAllocated(wishlistId,bankB.Id));Equal(0,d.Transactions.Count);});
Test("reassignment requires free destination funds",()=>{var d=WishlistService.Allocate(WishlistFunded(),wishlistId,bankA.Id,1_000_000,wishlistDate);Reject(()=>WishlistService.Move(d,wishlistId,bankA.Id,bankB.Id,1_500_000,0,wishlistDate,false));});
Test("reassignment cannot charge an admin fee",()=>Reject(()=>WishlistService.Move(WishlistFunded(),wishlistId,bankA.Id,bankB.Id,500_000,1,wishlistDate,false)));
Test("transfer fee cannot be paid from allocated money",()=>{var d=WishlistService.Allocate(WishlistFunded(),wishlistId,bankA.Id,4_000_000,wishlistDate);Reject(()=>WishlistService.Move(d,wishlistId,bankA.Id,bankB.Id,1_000_000,1,wishlistDate,true));});
Test("release returns funds to free balance without changing bank balance",()=>{var d=WishlistService.Release(WishlistFunded(),wishlistId,bankA.Id,500_000,wishlistDate);Equal(500_000m,d.WishlistAllocated(wishlistId,bankA.Id));Equal(4_500_000m,d.AvailableBalance(bankA.Id));Equal(5_000_000m,d.Balance(bankA.Id));});
Test("release cannot exceed allocation",()=>Reject(()=>WishlistService.Release(WishlistFunded(),wishlistId,bankA.Id,1_000_001,wishlistDate)));
Test("purchase requires allocation in the payment account even with sufficient bank balance",()=>Reject(()=>WishlistService.Purchase(WishlistFunded(),wishlistId,bankB.Id,food.Id,4_900_000,0,wishlistDate)));
Test("purchase records linked expense and retains remainder",()=>{var d=WishlistBought();Equal(WishlistStatus.Purchased,d.WishlistState(wishlistId));Equal(100_000m,d.WishlistAllocated(wishlistId));Equal(997_000m,d.Balance(bankB.Id));Equal(4_905_500m,d.Totals(start,new(2026,10,31)).Expense);});
Test("purchase category is mandatory",()=>Reject(()=>WishlistService.Purchase(WishlistMoved(),wishlistId,bankB.Id,Guid.Empty,4_900_000,0,wishlistDate)));
Test("purchase admin fee uses only available balance",()=>Reject(()=>WishlistService.Purchase(WishlistMoved(),wishlistId,bankB.Id,food.Id,4_900_000,1_000_001,wishlistDate)));
Test("release balance does not record an income and keeps Purchased status",()=>{var d=WishlistBought();var balance=d.Balance(bankA.Id);var count=d.Transactions.Count;d=WishlistService.ReleaseBalance(d,wishlistId,wishlistDate);Equal(balance,d.Balance(bankA.Id));Equal(count,d.Transactions.Count);Equal(0m,d.WishlistAllocated(wishlistId));Equal(WishlistStatus.Purchased,d.WishlistState(wishlistId));});
Test("close is blocked until remaining allocations are released",()=>Reject(()=>WishlistService.Close(WishlistBought(),wishlistId,wishlistDate)));
Test("even zero remainder requires explicit close",()=>{var d=WishlistService.Purchase(WishlistMoved(),wishlistId,bankB.Id,food.Id,4_900_000,0,wishlistDate);d=WishlistService.ReleaseBalance(d,wishlistId,wishlistDate);Equal(WishlistStatus.Purchased,d.WishlistState(wishlistId));d=WishlistService.Close(d,wishlistId,wishlistDate);Equal(WishlistStatus.Closed,d.WishlistState(wishlistId));});
Test("cancel releases all accounts after confirmation in UI",()=>{var d=WishlistService.Cancel(WishlistFunded(),wishlistId,wishlistDate);Equal(WishlistStatus.Cancelled,d.WishlistState(wishlistId));Equal(0m,d.WishlistAllocated(wishlistId));Equal(5_000_000m,d.AvailableBalance(bankA.Id));Equal(0,d.Transactions.Count);Reject(()=>WishlistService.Allocate(d,wishlistId,bankA.Id,1,wishlistDate));});
Test("target changes retain allocations including overfunded amounts",()=>{var d=WishlistFunded();d=WishlistService.Edit(d,Laptop() with {TargetAmount=4_000_000});Equal(5_000_000m,d.WishlistAllocated(wishlistId));Equal(4_000_000m,d.Wishlists[0].TargetAmount);});
Test("correct closed purchase restores only last purchase amount and its fee to free balance",()=>{var d=WishlistBought();d=WishlistService.ReleaseBalance(d,wishlistId,wishlistDate);d=WishlistService.Close(d,wishlistId,wishlistDate);d=WishlistService.CorrectPurchase(d,wishlistId,wishlistDate);Equal(WishlistStatus.Planned,d.WishlistState(wishlistId));Equal(4_900_000m,d.WishlistAllocated(wishlistId));Equal(0m,d.WishlistAllocated(wishlistId,bankA.Id));Equal(5_900_000m,d.Balance(bankB.Id));Equal(1_000_000m,d.AvailableBalance(bankB.Id));Equal(2500m,d.Totals(start,new(2026,10,31)).Expense);});
Test("repurchase remains allocation only after correction",()=>{var d=WishlistService.CorrectPurchase(WishlistBought(),wishlistId,wishlistDate);Reject(()=>WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,5_000_000,0,wishlistDate));});
Test("purchase and transfer cannot be edited or deleted independently",()=>{var d=WishlistBought();var tx=d.Transactions.Last();Equal(true,d.IsWishlistTransaction(tx.Id));d.Transactions.Remove(tx);Reject(d.Validate);var moved=WishlistMoved();moved.Transactions[0]=moved.Transactions[0] with {Amount=800_000};Reject(moved.Validate);});
Test("funding history protects banks even after cancellation",()=>{var d=WishlistService.Cancel(WishlistFunded(),wishlistId,wishlistDate);d.Banks.RemoveAt(0);Reject(d.Validate);});
Test("failed operation preserves source document",()=>{var d=WishlistFunded();var before=JsonSerializer.Serialize(d);Reject(()=>WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,5_000_000,0,wishlistDate));Equal(before,JsonSerializer.Serialize(d));});
Test("copy isolates wishlist lists",()=>{var d=WishlistFunded();var copy=d.Copy();copy.Wishlists.Clear();copy.WishlistEvents.Clear();Equal(1,d.Wishlists.Count);Equal(3,d.WishlistEvents.Count);});
Test("old formats reject nonempty wishlist data",()=>{var d=WishlistFunded();d.Version=5;Reject(d.Validate);});
Test("wishlist amounts and malformed bank fields are rejected",()=>{var d=WishlistFunded();d.WishlistEvents[1]=d.WishlistEvents[1] with {Amount=1.001m};Reject(d.Validate);d=WishlistFunded();d.WishlistEvents[1]=d.WishlistEvents[1] with {ToBankId=null};Reject(d.Validate);});
Test("wishlist allocation date cannot move backwards after funding operations",()=>{var d=WishlistFunded();Reject(()=>WishlistService.Release(d,wishlistId,bankA.Id,1,wishlistDate.AddDays(-1)));});
Test("negative legacy balances remain allowed without wishlist funds",()=>{var d=Empty();d.Transactions=[Expense() with {Amount=4_000_000}];d.Validate();});

Test("duplicate purchase is rejected without duplicating the expense",()=>{var d=WishlistBought();var count=d.Transactions.Count;Reject(()=>WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,1,0,wishlistDate));Equal(count,d.Transactions.Count);});
Test("purchase fee cannot be forged into reserved money in serialized records",()=>{var d=WishlistService.Purchase(WishlistMoved(),wishlistId,bankB.Id,food.Id,4_900_000,0,wishlistDate);var e=d.WishlistEvents.Last();d.WishlistEvents[^1]=e with {Fee=2_000_000};d.Transactions[^1]=d.Transactions[^1] with {Fee=2_000_000};Reject(d.Validate);});
Test("deleting income cannot remove cash backing allocations",()=>{var d=WishlistEmpty();d.Transactions.Add(Income(1_000_000));d=WishlistService.Allocate(d,wishlistId,bankA.Id,6_000_000,wishlistDate);d.Transactions.Clear();Reject(d.Validate);});
Test("previous reservations block spending even after funds were later released",()=>{var d=WishlistService.Release(WishlistFunded(),wishlistId,bankA.Id,1_000_000,wishlistDate.AddDays(1));d.Transactions.Add(Expense() with {Date=wishlistDate,Amount=4_500_000});Reject(d.Validate);});
Test("corrected wishlist can be purchased and closed again",()=>{var d=WishlistService.CorrectPurchase(WishlistBought(),wishlistId,wishlistDate);d=WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,4_800_000,0,wishlistDate);Equal(200_000m,d.WishlistAllocated(wishlistId));d=WishlistService.ReleaseBalance(d,wishlistId,wishlistDate);d=WishlistService.Close(d,wishlistId,wishlistDate);Equal(WishlistStatus.Closed,d.WishlistState(wishlistId));});
Test("wishlist actions reject future dates",()=>Reject(()=>WishlistService.Allocate(WishlistEmpty(),wishlistId,bankA.Id,1,DateOnly.FromDateTime(DateTime.Today).AddDays(1))));
var correctedAllocationDate=new DateOnly(2026,9,25);
FinanceDocument AllocationDateFixture(){var d=WishlistEmpty();d.Banks=d.Banks.Select(b=>b with {OpeningDate=new(2026,9,1)}).ToList();return WishlistService.Allocate(d,wishlistId,bankA.Id,135_000,new(2026,10,26));}
Test("allocation date correction enables September transfer without duplicating allocation",()=>{
    var source=AllocationDateFixture();var activity=source.WishlistEvents.Last();var before=JsonSerializer.Serialize(source);
    Reject(()=>WishlistService.Move(source,wishlistId,bankA.Id,bankB.Id,135_000,2500,new(2026,9,27),true));
    var d=WishlistService.EditAllocationDate(source,activity.Id,correctedAllocationDate);
    Equal(before,JsonSerializer.Serialize(source));Equal(activity with {Date=correctedAllocationDate},d.WishlistEvents.Last());Equal(source.WishlistEvents.Count,d.WishlistEvents.Count);
    Equal(135_000m,d.WishlistAllocated(wishlistId,bankA.Id,correctedAllocationDate));Equal(0m,d.WishlistAllocated(wishlistId,bankA.Id,correctedAllocationDate.AddDays(-1)));
    Equal(source.Balance(bankA.Id),d.Balance(bankA.Id));Equal(0,d.Transactions.Count);Equal(source.WishlistAllocated(wishlistId),d.WishlistAllocated(wishlistId));
    d=WishlistService.Move(d,wishlistId,bankA.Id,bankB.Id,135_000,2500,new(2026,9,27),true);Equal(135_000m,d.WishlistAllocated(wishlistId,bankB.Id));Equal(1,d.Transactions.Count);
});
Test("allocation date cannot predate bank opening balance",()=>{var d=AllocationDateFixture();Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents.Last().Id,new(2026,8,31)));});
Test("allocation date cannot be empty or in the future",()=>{var d=AllocationDateFixture();var id=d.WishlistEvents.Last().Id;Reject(()=>WishlistService.EditAllocationDate(d,id,default));Reject(()=>WishlistService.EditAllocationDate(d,id,DateOnly.FromDateTime(DateTime.Today).AddDays(1)));});
Test("allocation date correction requires an existing Allocate entry",()=>{var d=AllocationDateFixture();Reject(()=>WishlistService.EditAllocationDate(d,Guid.NewGuid(),correctedAllocationDate));Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents[0].Id,correctedAllocationDate));});
Test("backdated allocation cannot reserve money before the supporting income",()=>{var d=WishlistEmpty();d.Banks[0]=d.Banks[0] with {OpeningBalance=0};d.Transactions.Add(Income(135_000));d=WishlistService.Allocate(d,wishlistId,bankA.Id,135_000,wishlistDate);Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents.Last().Id,new(2026,10,22)));});
Test("backdated allocation cannot overlap earlier reservations for another wishlist",()=>{var d=WishlistEmpty();var other=Laptop() with {Id=Guid.NewGuid(),Name="Other"};d=WishlistService.Create(d,other);d=WishlistService.Allocate(d,other.Id,bankA.Id,5_000_000,new(2026,10,10));d=WishlistService.Release(d,other.Id,bankA.Id,5_000_000,new(2026,10,20));d=WishlistService.Allocate(d,wishlistId,bankA.Id,135_000,wishlistDate);var before=JsonSerializer.Serialize(d);Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents.Last().Id,new(2026,10,15)));Equal(before,JsonSerializer.Serialize(d));});
Test("allocation date must remain before the following funds activity",()=>{var d=WishlistMoved();Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents[1].Id,wishlistDate.AddDays(1)));});
Test("allocation date must remain after the preceding funds activity",()=>{var d=WishlistFunded();Reject(()=>WishlistService.EditAllocationDate(d,d.WishlistEvents[2].Id,wishlistDate.AddDays(-1)));});
Test("correcting older allocations in order permits moving both to earlier dates",()=>{var d=WishlistFunded();d=WishlistService.EditAllocationDate(d,d.WishlistEvents[1].Id,new(2026,10,10));d=WishlistService.EditAllocationDate(d,d.WishlistEvents[2].Id,new(2026,10,10));Equal(5_000_000m,d.WishlistAllocated(wishlistId,null,new(2026,10,10)));});
Test("allocation date correction preserves linked purchase transfer fees and closed status",()=>{var d=WishlistService.Close(WishlistService.ReleaseBalance(WishlistBought(),wishlistId,wishlistDate),wishlistId,wishlistDate);var tx=JsonSerializer.Serialize(d.Transactions);d=WishlistService.EditAllocationDate(d,d.WishlistEvents[1].Id,new(2026,10,10));Equal(tx,JsonSerializer.Serialize(d.Transactions));Equal(WishlistStatus.Closed,d.WishlistState(wishlistId));Equal(0m,d.WishlistAllocated(wishlistId));});
Test("transfer and release dates cannot be changed through allocation correction",()=>{var d=WishlistService.ReleaseBalance(WishlistBought(),wishlistId,wishlistDate);foreach(var e in d.WishlistEvents.Where(e=>e.Kind is WishlistEventKind.Transferred or WishlistEventKind.Purchased or WishlistEventKind.Released))Reject(()=>WishlistService.EditAllocationDate(d,e.Id,correctedAllocationDate));});
Test("unchanged allocation date preserves financial data",()=>{var d=AllocationDateFixture();Equal(JsonSerializer.Serialize(d),JsonSerializer.Serialize(WishlistService.EditAllocationDate(d,d.WishlistEvents.Last().Id,d.WishlistEvents.Last().Date)));});
var folder = Path.Combine(Path.GetTempPath(),"MyPiggyBankTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
Test("wishlist creation accepts the historical planned date without initial funds",()=>{var d=WishlistService.Create(new FinanceDocument(),Laptop(),new(2026,9,25));Equal(new DateOnly(2026,9,25),d.WishlistEvents.Single().Date);Equal(WishlistEventKind.Created,d.WishlistEvents.Single().Kind);Equal(0m,d.WishlistAllocated(wishlistId));});
Test("new planned date rejects empty and future values",()=>{Reject(()=>WishlistService.Create(new FinanceDocument(),Laptop(),default(DateOnly)));Reject(()=>WishlistService.Create(new FinanceDocument(),Laptop(),DateOnly.FromDateTime(DateTime.Today).AddDays(1)));});
Test("wishlist edit records selected update date rather than today's date",()=>{var d=WishlistService.Create(new FinanceDocument(),Laptop(),new(2026,9,25));d=WishlistService.Edit(d,Laptop() with {TargetAmount=4_900_000},new(2026,9,26));Equal(new DateOnly(2026,9,26),d.WishlistEvents.Last().Date);Equal(WishlistEventKind.Edited,d.WishlistEvents.Last().Kind);Equal(4_900_000m,d.Wishlists.Single().TargetAmount);});
Test("editing wishlist details can atomically correct planned and update dates",()=>{var d=WishlistEmpty();var before=JsonSerializer.Serialize(d);var result=WishlistService.Edit(d,Laptop() with {Name="Historical laptop"},new(2026,9,26),new(2026,9,25));Equal(before,JsonSerializer.Serialize(d));Equal(new DateOnly(2026,9,25),result.WishlistEvents.First().Date);Equal(new DateOnly(2026,9,26),result.WishlistEvents.Last().Date);Equal("Historical laptop",result.Wishlists.Single().Name);});
Test("invalid update date preserves details and creation date",()=>{var d=WishlistEmpty();var before=JsonSerializer.Serialize(d);Reject(()=>WishlistService.Edit(d,Laptop() with {Name="Rejected"},new(2026,9,24),new(2026,9,25)));Equal(before,JsonSerializer.Serialize(d));});
Test("created and updated dates are independently correctable on legacy data",()=>{var d=WishlistService.Edit(WishlistFunded(),Laptop());var count=d.WishlistEvents.Count;var created=d.WishlistEvents.First();var edited=d.WishlistEvents.Last();d=WishlistService.EditActivityDate(d,created.Id,new(2026,9,25));d=WishlistService.EditActivityDate(d,edited.Id,new(2026,9,26));Equal(count,d.WishlistEvents.Count);Equal(created with {Date=new(2026,9,25)},d.WishlistEvents.First());Equal(edited with {Date=new(2026,9,26)},d.WishlistEvents.Last());Equal(5_000_000m,d.WishlistAllocated(wishlistId));});
Test("update correction cannot predate planned date",()=>{var d=WishlistService.Edit(WishlistEmpty(),Laptop());Reject(()=>WishlistService.EditActivityDate(d,d.WishlistEvents.Last().Id,new(2026,9,25)));});
Test("planned correction cannot follow existing update date",()=>{var d=WishlistService.Create(new FinanceDocument(),Laptop(),new(2026,9,25));d=WishlistService.Edit(d,Laptop(),new(2026,9,26));Reject(()=>WishlistService.EditActivityDate(d,d.WishlistEvents.First().Id,new(2026,9,27)));});
Test("metadata correction rejects missing default and future dates",()=>{var d=WishlistEmpty();var id=d.WishlistEvents.First().Id;Reject(()=>WishlistService.EditActivityDate(d,Guid.NewGuid(),new(2026,9,25)));Reject(()=>WishlistService.EditActivityDate(d,id,default));Reject(()=>WishlistService.EditActivityDate(d,id,DateOnly.FromDateTime(DateTime.Today).AddDays(1)));});
Test("created date correction on purchased wishlist preserves financial records and status",()=>{var d=WishlistBought();var transactions=JsonSerializer.Serialize(d.Transactions);var funds=d.WishlistEvents.Where(e=>e.Amount>0).ToArray();d=WishlistService.EditActivityDate(d,d.WishlistEvents.First().Id,new(2026,9,25));Equal(transactions,JsonSerializer.Serialize(d.Transactions));Equal(true,funds.SequenceEqual(d.WishlistEvents.Where(e=>e.Amount>0)));Equal(WishlistStatus.Purchased,d.WishlistState(wishlistId));Equal(100_000m,d.WishlistAllocated(wishlistId));});
Test("generic date editing preserves funding chronology for transfers and allocations",()=>{var d=WishlistMoved();var tx=d.WishlistEvents.Last();Reject(()=>WishlistService.EditActivityDate(d,tx.Id,new(2026,10,23)));Reject(()=>WishlistService.EditActivityDate(d,d.WishlistEvents[1].Id,new(2026,10,25)));d=WishlistService.EditActivityDate(d,d.WishlistEvents[1].Id,new(2026,10,10));Equal(new DateOnly(2026,10,10),d.WishlistEvents[1].Date);});
FinanceDocument PurchaseDateFixture(){
    var d=WishlistService.Create(new FinanceDocument{Banks=[bankA with{OpeningDate=new(2026,9,1),OpeningBalance=1_000_000},bankB with{Name="Jago",OpeningDate=new(2026,9,1),OpeningBalance=10_000}],Categories=[salary,food]},Laptop() with{TargetAmount=172_192},new(2026,9,25));
    d=WishlistService.Allocate(d,wishlistId,bankA.Id,200_000,new(2026,9,25));d=WishlistService.Move(d,wishlistId,bankA.Id,bankB.Id,200_000,2500,new(2026,9,27),true);return WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,172_192,1000,new(2026,10,11));
}
Test("purchase date correction moves linked expense and fee atomically without changing current totals",()=>{
    var source=PurchaseDateFixture();var before=JsonSerializer.Serialize(source);var purchase=source.WishlistEvents.Last();var tx=source.Transactions.Last();var at=new DateOnly(2026,10,12);
    var d=WishlistService.EditActivityDate(source,purchase.Id,at);Equal(before,JsonSerializer.Serialize(source));Equal(purchase with{Date=at},d.WishlistEvents.Last());Equal(tx with{Date=at},d.Transactions.Last());Equal(source.Transactions.Count,d.Transactions.Count);Equal(source.WishlistEvents.Count,d.WishlistEvents.Count);Equal(source.WishlistEvents[2],d.WishlistEvents[2]);
    Equal(WishlistStatus.Purchased,d.WishlistState(wishlistId));Equal(27_808m,d.WishlistAllocated(wishlistId));Equal(source.Balance(bankB.Id),d.Balance(bankB.Id));Equal(source.AvailableBalance(bankB.Id),d.AvailableBalance(bankB.Id));Equal(0m,d.Totals(new(2026,10,11),new(2026,10,11)).Expense);Equal(173_192m,d.Totals(at,at).Expense);
    Equal(200_000m,d.WishlistAllocated(wishlistId,bankB.Id,new(2026,10,11)));Equal(210_000m,d.Balance(bankB.Id,new(2026,10,11)));Equal(27_808m,d.WishlistAllocated(wishlistId,bankB.Id,at));Equal(36_808m,d.Balance(bankB.Id,at));
});
Test("purchase date correction can move earlier after funding",()=>{var d=PurchaseDateFixture();d=WishlistService.EditPurchaseDate(d,d.WishlistEvents.Last().Id,new(2026,10,10));Equal(173_192m,d.Totals(new(2026,10,10),new(2026,10,10)).Expense);});
Test("purchase date cannot precede its funding transfer",()=>{var d=PurchaseDateFixture();Reject(()=>WishlistService.EditPurchaseDate(d,d.WishlistEvents.Last().Id,new(2026,9,26)));});
Test("purchase date cannot follow already recorded release",()=>{var d=WishlistService.ReleaseBalance(PurchaseDateFixture(),wishlistId,new(2026,10,11));var purchase=d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Purchased);var before=JsonSerializer.Serialize(d);Reject(()=>WishlistService.EditPurchaseDate(d,purchase.Id,new(2026,10,12)));Equal(before,JsonSerializer.Serialize(d));});
Test("closed wishlist purchase date is editable when later release and close permit it",()=>{var d=WishlistService.ReleaseBalance(PurchaseDateFixture(),wishlistId,new(2026,10,20));d=WishlistService.Close(d,wishlistId,new(2026,10,20));var purchase=d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Purchased);var count=d.WishlistEvents.Count;d=WishlistService.EditPurchaseDate(d,purchase.Id,new(2026,10,12));Equal(WishlistStatus.Closed,d.WishlistState(wishlistId));Equal(0m,d.WishlistAllocated(wishlistId));Equal(count,d.WishlistEvents.Count);});
Test("reversed purchase date can be corrected without recreating expense",()=>{var d=PurchaseDateFixture();var purchase=d.WishlistEvents.Last();d=WishlistService.CorrectPurchase(d,wishlistId,new(2026,10,20));var count=d.Transactions.Count;d=WishlistService.EditPurchaseDate(d,purchase.Id,new(2026,10,12));Equal(count,d.Transactions.Count);Equal(false,d.Transactions.Any(t=>t.Id==purchase.TransactionId));Equal(WishlistStatus.Planned,d.WishlistState(wishlistId));Equal(200_000m,d.WishlistAllocated(wishlistId));Equal(new DateOnly(2026,10,12),d.WishlistEvents.Single(e=>e.Id==purchase.Id).Date);});
Test("purchase date correction cannot spend admin fees before supporting income",()=>{var d=PurchaseDateFixture();d.Banks[1]=d.Banks[1] with{OpeningBalance=0};d.Transactions.Add(Income(1000) with{BankId=bankB.Id,Date=new(2026,10,11)});d.Validate();Reject(()=>WishlistService.EditPurchaseDate(d,d.WishlistEvents.Last().Id,new(2026,10,10)));});
Test("purchase date correction rejects missing default future and nonpurchase entries",()=>{var d=PurchaseDateFixture();var id=d.WishlistEvents.Last().Id;Reject(()=>WishlistService.EditPurchaseDate(d,Guid.NewGuid(),new(2026,10,12)));Reject(()=>WishlistService.EditPurchaseDate(d,id,default));Reject(()=>WishlistService.EditPurchaseDate(d,id,DateOnly.FromDateTime(DateTime.Today).AddDays(1)));Reject(()=>WishlistService.EditPurchaseDate(d,d.WishlistEvents[1].Id,new(2026,10,12)));});
Test("correct purchase still restores the exact amount after purchase date correction",()=>{var d=PurchaseDateFixture();d=WishlistService.EditPurchaseDate(d,d.WishlistEvents.Last().Id,new(2026,10,12));d=WishlistService.CorrectPurchase(d,wishlistId,new(2026,10,20));Equal(WishlistStatus.Planned,d.WishlistState(wishlistId));Equal(200_000m,d.WishlistAllocated(wishlistId));Equal(210_000m,d.Balance(bankB.Id));Equal(1,d.Transactions.Count);});
Test("purchase date edit cannot repair a missing linked expense silently",()=>{var d=PurchaseDateFixture();d.Transactions.RemoveAt(d.Transactions.Count-1);Reject(()=>WishlistService.EditPurchaseDate(d,d.WishlistEvents.Last().Id,new(2026,10,12)));});
FinanceDocument BeforeDatePurchase(){var d=PurchaseDateFixture();d.Transactions.RemoveAt(d.Transactions.Count-1);d.WishlistEvents.RemoveAt(d.WishlistEvents.Count-1);d.Validate();return d;}
(FinanceDocument Document,WishlistEvent Activity) DateActivityFixture(WishlistEventKind kind){
    var d=PurchaseDateFixture();
    if(kind==WishlistEventKind.Edited)d=WishlistService.Edit(BeforeDatePurchase(),d.Wishlists.Single(),new(2026,10,1));
    if(kind==WishlistEventKind.Reassigned)d=WishlistService.Move(BeforeDatePurchase(),wishlistId,bankB.Id,bankA.Id,50_000,0,new(2026,10,1),false);
    if(kind==WishlistEventKind.Released)d=WishlistService.ReleaseBalance(d,wishlistId,new(2026,10,20));
    if(kind==WishlistEventKind.Cancelled)d=WishlistService.Cancel(BeforeDatePurchase(),wishlistId,new(2026,10,20));
    if(kind==WishlistEventKind.Closed)d=WishlistService.Close(WishlistService.ReleaseBalance(d,wishlistId,new(2026,10,20)),wishlistId,new(2026,10,21));
    if(kind==WishlistEventKind.PurchaseCorrected)d=WishlistService.CorrectPurchase(d,wishlistId,new(2026,10,20));
    return(d,d.WishlistEvents.Last(e=>e.Kind==kind));
}
foreach(var kind in Enum.GetValues<WishlistEventKind>()){
    Test("all wishlist activity kinds support date correction: "+kind,()=>{
        var (source,activity)=DateActivityFixture(kind);var before=JsonSerializer.Serialize(source);var at=activity.Date.AddDays(kind==WishlistEventKind.Created?-1:1);
        var d=WishlistService.EditActivityDate(source,activity.Id,at);Equal(before,JsonSerializer.Serialize(source));Equal(activity with{Date=at},d.WishlistEvents.Single(e=>e.Id==activity.Id));Equal(source.WishlistEvents.Count,d.WishlistEvents.Count);Equal(source.Transactions.Count,d.Transactions.Count);Equal(source.WishlistState(wishlistId),d.WishlistState(wishlistId));Equal(source.WishlistAllocated(wishlistId),d.WishlistAllocated(wishlistId));
        foreach(var b in source.Banks){Equal(source.Balance(b.Id),d.Balance(b.Id));Equal(source.AvailableBalance(b.Id),d.AvailableBalance(b.Id));}
        foreach(var tx in source.Transactions)Equal(tx.Id==activity.TransactionId?tx with{Date=at}:tx,d.Transactions.Single(t=>t.Id==tx.Id));
        Equal(true,source.WishlistEvents.Where(e=>e.Id!=activity.Id).SequenceEqual(d.WishlistEvents.Where(e=>e.Id!=activity.Id)));
    });
    Test("all activity dates reject empty and future corrections: "+kind,()=>{var(d,e)=DateActivityFixture(kind);Reject(()=>WishlistService.EditActivityDate(d,e.Id,default));Reject(()=>WishlistService.EditActivityDate(d,e.Id,DateOnly.FromDateTime(DateTime.Today).AddDays(1)));});
}
Test("transfer date moves both bank legs reservation and fee together",()=>{var d=PurchaseDateFixture();var transfer=d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Transferred);d=WishlistService.EditActivityDate(d,transfer.Id,new(2026,9,28));Equal(1_000_000m,d.Balance(bankA.Id,new(2026,9,27)));Equal(10_000m,d.Balance(bankB.Id,new(2026,9,27)));Equal(200_000m,d.WishlistAllocated(wishlistId,bankA.Id,new(2026,9,27)));Equal(0m,d.WishlistAllocated(wishlistId,bankB.Id,new(2026,9,27)));Equal(797_500m,d.Balance(bankA.Id,new(2026,9,28)));Equal(210_000m,d.Balance(bankB.Id,new(2026,9,28)));Equal(2500m,d.Totals(new(2026,9,28),new(2026,9,28)).Expense);});
Test("transfer date cannot predate destination opening",()=>{var d=PurchaseDateFixture();d.Banks[1]=d.Banks[1] with{OpeningDate=new(2026,9,27)};d.Validate();Reject(()=>WishlistService.EditActivityDate(d,d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Transferred).Id,new(2026,9,26)));});
Test("transfer fee cannot move before supporting income",()=>{var d=PurchaseDateFixture();d.Banks[0]=d.Banks[0] with{OpeningBalance=200_000};d.Transactions.Add(Income(2500) with{Date=new(2026,9,27)});d.Validate();var before=JsonSerializer.Serialize(d);Reject(()=>WishlistService.EditActivityDate(d,d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Transferred).Id,new(2026,9,26)));Equal(before,JsonSerializer.Serialize(d));});
Test("release cannot move after spending which used the released funds",()=>{var (d,e)=DateActivityFixture(WishlistEventKind.Released);d.Transactions.Add(Expense() with{BankId=bankB.Id,Date=new(2026,10,20),Amount=30_000,Fee=0});d.Validate();Reject(()=>WishlistService.EditActivityDate(d,e.Id,new(2026,10,21)));});
Test("correction date cannot move after subsequent purchase",()=>{var(d,e)=DateActivityFixture(WishlistEventKind.PurchaseCorrected);d=WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,150_000,0,new(2026,10,21));Reject(()=>WishlistService.EditActivityDate(d,e.Id,new(2026,10,22)));});
Test("cancel date cannot precede its allocation releases",()=>{var(d,e)=DateActivityFixture(WishlistEventKind.Cancelled);Reject(()=>WishlistService.EditActivityDate(d,e.Id,new(2026,10,19)));});
Test("multiple release and close dates can be corrected in chronological order",()=>{var d=WishlistService.Purchase(WishlistFunded(),wishlistId,bankB.Id,food.Id,3_000_000,0,wishlistDate);d=WishlistService.ReleaseBalance(d,wishlistId,wishlistDate.AddDays(1));d=WishlistService.Close(d,wishlistId,wishlistDate.AddDays(2));foreach(var e in d.WishlistEvents.Where(e=>e.Kind==WishlistEventKind.Released).ToList())d=WishlistService.EditActivityDate(d,e.Id,wishlistDate);var close=d.WishlistEvents.Last();d=WishlistService.EditActivityDate(d,close.Id,wishlistDate.AddDays(1));Equal(WishlistStatus.Closed,d.WishlistState(wishlistId));Equal(0m,d.WishlistAllocated(wishlistId));Equal(2,d.WishlistEvents.Count(e=>e.Kind==WishlistEventKind.Released));});
foreach(var status in Enum.GetValues<WishlistStatus>())Test("target date can be edited and cleared in status "+status,()=>{var d=status switch{WishlistStatus.Planned=>BeforeDatePurchase(),WishlistStatus.Purchased=>PurchaseDateFixture(),WishlistStatus.Closed=>DateActivityFixture(WishlistEventKind.Closed).Document,_=>DateActivityFixture(WishlistEventKind.Cancelled).Document};var before=JsonSerializer.Serialize(d);var history=JsonSerializer.Serialize(d.WishlistEvents);var ledger=JsonSerializer.Serialize(d.Transactions);var at=DateOnly.FromDateTime(DateTime.Today).AddYears(1);var edited=WishlistService.EditTargetDate(d,wishlistId,at);Equal(before,JsonSerializer.Serialize(d));Equal(history,JsonSerializer.Serialize(edited.WishlistEvents));Equal(ledger,JsonSerializer.Serialize(edited.Transactions));Equal(status,edited.WishlistState(wishlistId));Equal<DateOnly?>(at,edited.Wishlists.Single().TargetDate);edited=WishlistService.EditTargetDate(edited,wishlistId,null);Equal<DateOnly?>(null,edited.Wishlists.Single().TargetDate);});
Test("target date editing rejects missing item and invalid date",()=>{var d=PurchaseDateFixture();Reject(()=>WishlistService.EditTargetDate(d,Guid.NewGuid(),new(2026,10,12)));Reject(()=>WishlistService.EditTargetDate(d,wishlistId,default(DateOnly)));});
foreach(var kind in Enum.GetValues<WishlistEventKind>())Test("date guidance matches chronology limits without changing data: "+kind,()=>{
    var(d,e)=DateActivityFixture(kind);var before=JsonSerializer.Serialize(d);var limits=WishlistPresentation.DateLimits(d,e,DateOnly.FromDateTime(DateTime.Today));
    Equal(true,e.Date>=limits.Earliest&&e.Date<=limits.Latest);Equal(before,JsonSerializer.Serialize(d));
    if(limits.Earliest>DateOnly.MinValue)Reject(()=>WishlistService.EditActivityDate(d,e.Id,limits.Earliest.AddDays(-1)));
    Reject(()=>WishlistService.EditActivityDate(d,e.Id,limits.Latest.AddDays(1)));
});
Test("allocation date guidance uses account opening when there is no earlier activity",()=>{var d=PurchaseDateFixture();var e=d.WishlistEvents.Single(e=>e.Kind==WishlistEventKind.Allocated);var limits=WishlistPresentation.DateLimits(d,e,new(2026,10,27));Equal(new DateOnly(2026,9,1),limits.Earliest);Equal(new DateOnly(2026,9,27),limits.Latest);});
Test("creation date guidance respects an earlier update without imposing a bank opening",()=>{var d=WishlistService.Create(new FinanceDocument(),Laptop(),new(2026,8,1));d=WishlistService.Edit(d,Laptop(),new(2026,8,3));var limits=WishlistPresentation.DateLimits(d,d.WishlistEvents.First(),new(2026,10,27));Equal(DateOnly.MinValue,limits.Earliest);Equal(new DateOnly(2026,8,3),limits.Latest);});
var debtId=Guid.NewGuid();
Debt PersonalDebt()=>new(debtId,"Personal loan","Alex",DateOnly.FromDateTime(DateTime.Today).AddDays(-1));
Transaction BorrowTx(decimal amount=1_000_000)=>Income(amount) with{Date=new(2026,10,4),Fee=2500,DebtId=debtId};
FinanceDocument BorrowedDebt()=>DebtService.SaveTransaction(Empty(),BorrowTx(),PersonalDebt());
FinanceDocument ToppedDebt()=>DebtService.SaveTransaction(BorrowedDebt(),Income(500_000) with{Date=new(2026,10,5),BankId=bankB.Id,DebtId=debtId});
FinanceDocument PartialDebt()=>DebtService.SaveTransaction(ToppedDebt(),Expense() with{Date=new(2026,10,10),BankId=bankB.Id,Amount=200_000,Fee=2500,DebtId=debtId});
FinanceDocument ZeroDebt()=>DebtService.SaveTransaction(PartialDebt(),Expense() with{Date=new(2026,10,20),Amount=1_300_000,DebtId=debtId});
Test("borrowing creates tracking and one original transaction only",()=>{var d=BorrowedDebt();Equal(1,d.Debts.Count);Equal(1,d.Transactions.Count);Equal(new DebtTotals(1_000_000,0),d.DebtAmounts(debtId));Equal(3_997_500m,d.Balance(bankA.Id));Equal(DebtStatus.Open,d.DebtState(debtId));});
Test("tracking existing income leaves cash report and transaction ID unchanged",()=>{var d=Empty();var tx=BorrowTx() with{DebtId=null};d.Transactions.Add(tx);var before=d.Balance(bankA.Id);var totals=d.Totals(start,new(2026,10,31));var tagged=DebtService.SaveTransaction(d,tx with{DebtId=debtId},PersonalDebt());Equal(before,tagged.Balance(bankA.Id));Equal(totals,tagged.Totals(start,new(2026,10,31)));Equal(tx with{DebtId=debtId},tagged.Transactions.Single());Equal(1,tagged.Transactions.Count);Equal(0,d.Debts.Count);});
Test("income before and after fee have identical debt principal",()=>{var before=BorrowedDebt();var after=DebtService.SaveTransaction(Empty(),BorrowTx(997_500) with{IncomeBasis=IncomeAmountBasis.AfterFee},PersonalDebt());Equal(before.DebtAmounts(debtId),after.DebtAmounts(debtId));Equal(before.Balance(bankA.Id),after.Balance(bankA.Id));Equal(before.Totals(start,new(2026,10,31)),after.Totals(start,new(2026,10,31)));});
Test("additional borrowing can arrive in a different account",()=>{var d=ToppedDebt();Equal(1,d.Debts.Count);Equal(1_500_000m,d.DebtAmounts(debtId).Remaining);Equal(2_500_000m,d.Balance(bankB.Id));});
Test("payment reduces only principal while reports retain expense and fees",()=>{var d=PartialDebt();Equal(new DebtTotals(1_500_000,200_000),d.DebtAmounts(debtId));Equal(2_297_500m,d.Balance(bankB.Id));Equal((1_500_000m,205_000m),d.Totals(start,new(2026,10,31)));Equal(DebtStatus.PartiallyPaid,d.DebtState(debtId));});
Test("debt does not reserve or deduct available bank funds",()=>{var d=BorrowedDebt();Equal(d.Balance(bankA.Id),d.AvailableBalance(bankA.Id));Equal(0m,d.WishlistReserved(bankA.Id));});
Test("zero principal awaits manual confirmation",()=>{var d=ZeroDebt();Equal(0m,d.DebtAmounts(debtId).Remaining);Equal(DebtStatus.PendingConfirmation,d.DebtState(debtId));Equal(false,d.Debts.Single().IsPaid);});
Test("marking Paid and reopening never create financial transactions",()=>{var d=ZeroDebt();var ledger=JsonSerializer.Serialize(d.Transactions);d=DebtService.SetPaid(d,debtId,true);Equal(DebtStatus.Paid,d.DebtState(debtId));Equal(ledger,JsonSerializer.Serialize(d.Transactions));d=DebtService.SetPaid(d,debtId,false);Equal(DebtStatus.PendingConfirmation,d.DebtState(debtId));Equal(ledger,JsonSerializer.Serialize(d.Transactions));});
Test("positive principal cannot be marked Paid",()=>Reject(()=>DebtService.SetPaid(PartialDebt(),debtId,true)));
Test("Paid debt rejects additional borrowing until Reopen",()=>{var d=DebtService.SetPaid(ZeroDebt(),debtId,true);var before=JsonSerializer.Serialize(d);var tx=BorrowTx(300_000) with{Date=new(2026,10,21),Fee=0};Reject(()=>DebtService.SaveTransaction(d,tx));Equal(before,JsonSerializer.Serialize(d));d=DebtService.SetPaid(d,debtId,false);d=DebtService.SaveTransaction(d,tx);Equal(300_000m,d.DebtAmounts(debtId).Remaining);Equal(DebtStatus.PartiallyPaid,d.DebtState(debtId));});
Test("overpayment is rejected atomically",()=>{var d=PartialDebt();var before=JsonSerializer.Serialize(d);Reject(()=>DebtService.SaveTransaction(d,Expense() with{Date=new(2026,10,21),Amount=1_300_001,DebtId=debtId}));Equal(before,JsonSerializer.Serialize(d));});
Test("payment cannot precede supporting borrowing",()=>Reject(()=>DebtService.SaveTransaction(BorrowedDebt(),Expense() with{Date=new(2026,10,3),Amount=100_000,DebtId=debtId})));
Test("same-day borrowing and payment use date aggregates",()=>{var tx=BorrowTx() with{Fee=0};var d=Empty();d.Debts.Add(PersonalDebt());d.Transactions=[Expense() with{Date=tx.Date,Amount=1_000_000,DebtId=debtId},tx];d.Validate();Equal(0m,d.DebtAmounts(debtId).Remaining);});
Test("historic date edit validates later repayments",()=>{var d=PartialDebt();var loan=d.Transactions.Single(t=>t.DebtId==debtId&&t.Kind==TransactionKind.Income&&t.BankId==bankA.Id);var top=d.Transactions.Single(t=>t.Kind==TransactionKind.Income&&t.BankId==bankB.Id);d=DebtService.SaveTransaction(d,top with{Date=new(2026,10,15)});Reject(()=>DebtService.SaveTransaction(d,loan with{Date=new(2026,10,16)}));});
Test("future debt records are rejected while ordinary scheduled transactions remain valid",()=>{var future=DateOnly.FromDateTime(DateTime.Today).AddDays(1);Reject(()=>DebtService.SaveTransaction(Empty(),BorrowTx() with{Date=future},PersonalDebt()));var d=Empty();d.Transactions.Add(Income() with{Date=future});d.Validate();});
Test("debt tracking cannot be added to own-account transfers",()=>Reject(()=>DebtService.SaveTransaction(BorrowedDebt(),Transfer() with{DebtId=debtId})));
Test("a debt must have a real recorded borrowing",()=>{var d=Empty();d.Debts.Add(PersonalDebt());Reject(d.Validate);d.Transactions.Add(Expense() with{Amount=100,DebtId=debtId});Reject(d.Validate);});
Test("last borrowing cannot be deleted or unlinked leaving an orphan",()=>{var d=BorrowedDebt();var tx=d.Transactions.Single();Reject(()=>DebtService.SaveTransaction(d,tx with{DebtId=null}));d.Transactions.Clear();Reject(d.Validate);});
Test("deleting a top-up revalidates all later payment dates",()=>{var d=ZeroDebt();d.Transactions.RemoveAll(t=>t.Kind==TransactionKind.Income&&t.BankId==bankB.Id);Reject(d.Validate);});
Test("untracking an expense retains its ordinary bank effect",()=>{var d=PartialDebt();var payment=d.Transactions.Last();var cash=d.Balance(bankB.Id);var totals=d.Totals(start,new(2026,10,31));d=DebtService.SaveTransaction(d,payment with{DebtId=null});Equal(0m,d.DebtAmounts(debtId).Paid);Equal(cash,d.Balance(bankB.Id));Equal(totals,d.Totals(start,new(2026,10,31)));});
Test("moving a payment to another debt validates both debts",()=>{var d=PartialDebt();var other=PersonalDebt() with{Id=Guid.NewGuid(),Name="Second"};d=DebtService.SaveTransaction(d,Income(100_000) with{Date=new(2026,10,2),DebtId=other.Id},other);var payment=d.Transactions.Single(t=>t.Kind==TransactionKind.Expense);var before=JsonSerializer.Serialize(d);Reject(()=>DebtService.SaveTransaction(d,payment with{DebtId=other.Id}));Equal(before,JsonSerializer.Serialize(d));var loan=d.Transactions.Single(t=>t.DebtId==other.Id);d=DebtService.SaveTransaction(d,loan with{Amount=300_000});d=DebtService.SaveTransaction(d,payment with{DebtId=other.Id});Equal(1_500_000m,d.DebtAmounts(debtId).Remaining);Equal(100_000m,d.DebtAmounts(other.Id).Remaining);});
Test("editing borrowing amount cannot invalidate existing repayments",()=>{var d=ZeroDebt();var tx=d.Transactions.First();Reject(()=>DebtService.SaveTransaction(d,tx with{Amount=100_000}));});
Test("Paid status permits fee-only edits that retain the same principal",()=>{var d=DebtService.SetPaid(ZeroDebt(),debtId,true);var tx=d.Transactions.First();d=DebtService.SaveTransaction(d,tx with{Fee=3000});Equal(DebtStatus.Paid,d.DebtState(debtId));Equal(0m,d.DebtAmounts(debtId).Remaining);});
Test("Paid status prevents unlinking a payment that restores outstanding debt",()=>{var d=DebtService.SetPaid(ZeroDebt(),debtId,true);Reject(()=>DebtService.SaveTransaction(d,d.Transactions.Last() with{DebtId=null}));});
Test("debt details are editable in every lifecycle state",()=>{foreach(var source in new[]{BorrowedDebt(),PartialDebt(),ZeroDebt(),DebtService.SetPaid(ZeroDebt(),debtId,true)}){var tx=JsonSerializer.Serialize(source.Transactions);var state=source.DebtState(debtId);var d=DebtService.EditDetails(source,debtId,"Renamed loan","Sam",DateOnly.FromDateTime(DateTime.Today).AddYears(1));Equal(state,d.DebtState(debtId));Equal(tx,JsonSerializer.Serialize(d.Transactions));Equal("Renamed loan · Sam",d.DebtLabel(debtId));}});
Test("overdue flag applies only to principal still outstanding",()=>{Equal(true,BorrowedDebt().DebtOverdue(debtId));Equal(false,ZeroDebt().DebtOverdue(debtId));var d=DebtService.EditDetails(BorrowedDebt(),debtId,"Personal loan","Alex",null);Equal(false,d.DebtOverdue(debtId));});
Test("Remove Tracking detaches all links but preserves ledger fields and reports",()=>{var d=PartialDebt();var expected=d.Transactions.Select(t=>t with{DebtId=null}).ToList();var balance=d.Banks.Sum(b=>d.Balance(b.Id));var totals=d.Totals(start,new(2026,10,31));d=DebtService.RemoveTracking(d,debtId);Equal(0,d.Debts.Count);Equal(true,expected.SequenceEqual(d.Transactions));Equal(balance,d.Banks.Sum(b=>d.Balance(b.Id)));Equal(totals,d.Totals(start,new(2026,10,31)));});
Test("Remove Tracking also works for a Paid debt",()=>{var d=DebtService.RemoveTracking(DebtService.SetPaid(ZeroDebt(),debtId,true),debtId);Equal(0,d.Debts.Count);Equal(4,d.Transactions.Count);Equal(true,d.Transactions.All(t=>t.DebtId is null));});
Test("debt metadata validates required names lender dates and IDs",()=>{foreach(var bad in new[]{PersonalDebt() with{Name=""},PersonalDebt() with{Name=" Loan "},PersonalDebt() with{Lender=""},PersonalDebt() with{DueDate=default(DateOnly)},PersonalDebt() with{Id=Guid.Empty}})Reject(()=>DebtService.SaveTransaction(Empty(),BorrowTx() with{DebtId=bad.Id},bad));var d=BorrowedDebt();d.Debts.Add(d.Debts.Single());Reject(d.Validate);});
Test("debt links reject unknown IDs and unsupported document formats",()=>{Reject(()=>DebtService.SaveTransaction(Empty(),BorrowTx()));var d=BorrowedDebt();d.Version=6;Reject(d.Validate);var noMetadata=Empty();noMetadata.Version=6;noMetadata.Transactions.Add(BorrowTx());Reject(noMetadata.Validate);});
Test("invalid new borrowing leaves no metadata or transaction in source",()=>{var d=Empty();var before=JsonSerializer.Serialize(d);Reject(()=>DebtService.SaveTransaction(d,BorrowTx() with{CategoryId=null},PersonalDebt()));Equal(before,JsonSerializer.Serialize(d));});
Test("debt list and transaction edits are isolated by Copy",()=>{var d=BorrowedDebt();var copy=d.Copy();copy.Debts.Clear();copy.Transactions[0]=copy.Transactions[0] with{DebtId=null};Equal(1,d.Debts.Count);Equal<Guid?>(debtId,d.Transactions.Single().DebtId);});
Test("wishlist purchase cannot also be a debt payment",()=>{var d=DebtService.SaveTransaction(WishlistBought(),BorrowTx(8_000_000),PersonalDebt());var purchase=d.Transactions.Single(t=>t.Kind==TransactionKind.Expense);Reject(()=>DebtService.SaveTransaction(d,purchase with{DebtId=debtId}));});
Test("debt payment respects protected wishlist reservations",()=>{var d=DebtService.SaveTransaction(WishlistFunded(),BorrowTx(),PersonalDebt());d=WishlistService.Allocate(d,wishlistId,bankA.Id,4_900_000,wishlistDate);Reject(()=>DebtService.SaveTransaction(d,Expense() with{Amount=500_000,DebtId=debtId}));});
Test("historical debt totals use original transaction dates",()=>{var d=PartialDebt();Equal(new DebtTotals(0,0),d.DebtAmounts(debtId,new(2026,10,3)));Equal(new DebtTotals(1_000_000,0),d.DebtAmounts(debtId,new(2026,10,4)));Equal(new DebtTotals(1_500_000,200_000),d.DebtAmounts(debtId,new(2026,10,10)));});
Test("metadata actions reject a missing debt",()=>{var d=BorrowedDebt();var id=Guid.NewGuid();Reject(()=>DebtService.SetPaid(d,id,true));Reject(()=>DebtService.EditDetails(d,id,"A","B",null));Reject(()=>DebtService.RemoveTracking(d,id));});
SelfDebtChecks.Run(Test);
BorrowTransferChecks.Run(Test);
UpdateChannelChecks.Run(Test);
Test("borrowing transfer respects Wishlist protected source funds",()=>{var d=WishlistFunded();var self=new Debt(Guid.NewGuid(),"Pocket money","",null,Kind:DebtKind.Self,SelfBankId:bankA.Id);d.Purposes=[debt];Reject(()=>DebtService.SaveTransaction(d,Transfer() with{Date=wishlistDate,Amount=4_500_000,Allocations=new[]{new TransferAllocation(debt.Id,4_500_000,self.Id,SelfDebtAction.Borrow)}},self));});
Test("Wishlist transfer cannot also start Self Debt",()=>{var d=WishlistMoved();var tx=d.Transactions.Single(t=>t.Kind==TransactionKind.Transfer);var self=new Debt(Guid.NewGuid(),"Pocket money","",null,Kind:DebtKind.Self,SelfBankId:tx.BankId);d.Purposes=[debt];Reject(()=>DebtService.SaveTransaction(d,tx with{Allocations=new[]{new TransferAllocation(debt.Id,tx.Amount,self.Id,SelfDebtAction.Borrow)}},self));});
Test("Wishlist purchase cannot also become Self Debt",()=>{var d=WishlistBought();var tx=d.Transactions.Single(t=>t.Kind==TransactionKind.Expense);var self=new Debt(Guid.NewGuid(),"Wishlist item","",null,Kind:DebtKind.Self,SelfBankId:tx.BankId);Reject(()=>DebtService.SaveTransaction(d,tx with{DebtId=self.Id},self));});
Test("Wishlist transfer cannot also repay Self Debt",()=>{var d=WishlistMoved();var self=new Debt(Guid.NewGuid(),"Small item","",null,Kind:DebtKind.Self,SelfBankId:bankB.Id);d=DebtService.SaveTransaction(d,Expense() with{Date=wishlistDate,BankId=bankB.Id,Amount=100,DebtId=self.Id},self);var tx=d.Transactions.Single(t=>t.Kind==TransactionKind.Transfer);d.Purposes=[debt];Reject(()=>DebtService.SaveTransaction(d,tx with{Allocations=new[]{new TransferAllocation(debt.Id,tx.Amount,self.Id)}}));});
Test("Self Debt repayment transfer respects Wishlist protected funds",()=>{var d=WishlistFunded();var self=new Debt(Guid.NewGuid(),"Small item","",null,Kind:DebtKind.Self,SelfBankId:bankB.Id);d=DebtService.SaveTransaction(d,Expense() with{Date=wishlistDate,BankId=bankB.Id,Amount=100,DebtId=self.Id},self);d.Purposes=[debt];Reject(()=>DebtService.SaveTransaction(d,Transfer() with{Date=wishlistDate,Amount=100,Fee=5_000_000,Allocations=new[]{new TransferAllocation(debt.Id,100,self.Id)}}));});
try
{
    var path = Path.Combine(folder,"test.mpb");
    Test("debt tracking and manual settlement survive manual Save and Open",()=>{var file=Path.Combine(folder,"debt.mpb");var session=new DocumentSession();session.Apply(ZeroDebt());session.Save(file);var bytes=File.ReadAllBytes(file);session.Apply(DebtService.SetPaid(session.Document,debtId,true));Equal(true,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));session.Save(file);var reopened=new DocumentSession();reopened.Open(file);Equal(FinanceDocument.CurrentVersion,reopened.Document.Version);Equal(DebtStatus.Paid,reopened.Document.DebtState(debtId));Equal(false,reopened.Dirty);Equal(4,reopened.Document.Transactions.Count);Equal(0m,reopened.Document.DebtAmounts(debtId).Remaining);});
    Test("format six opens without invented debt links or source rewrite",()=>{var file=Path.Combine(folder,"format-six.mpb");var d=WishlistBought();d.Version=6;var node=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(d))!;node.AsObject().Remove("Debts");foreach(var tx in node["Transactions"]!.AsArray())tx!.AsObject().Remove("DebtId");File.WriteAllText(file,node.ToJsonString());var bytes=File.ReadAllBytes(file);var session=new DocumentSession();session.Open(file);Equal(FinanceDocument.CurrentVersion,session.Document.Version);Equal(0,session.Document.Debts.Count);Equal(true,session.Document.Transactions.All(t=>t.DebtId is null));Equal(WishlistStatus.Purchased,session.Document.WishlistState(wishlistId));Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));Equal(false,session.Dirty);});
    Test("all activity date corrections survive manual save and reopen",()=>{foreach(var kind in Enum.GetValues<WishlistEventKind>()){var(d,e)=DateActivityFixture(kind);var file=Path.Combine(folder,"date-"+kind+".mpb");var session=new DocumentSession();session.Apply(d);session.Save(file);var bytes=File.ReadAllBytes(file);var at=e.Date.AddDays(kind==WishlistEventKind.Created?-1:1);session.Apply(WishlistService.EditActivityDate(session.Document,e.Id,at));Equal(true,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));session.Save(file);var reopened=new DocumentSession();reopened.Open(file);Equal(false,reopened.Dirty);Equal(FinanceDocument.CurrentVersion,reopened.Document.Version);Equal(at,reopened.Document.WishlistEvents.Single(x=>x.Id==e.Id).Date);Equal(JsonSerializer.Serialize(session.Document),JsonSerializer.Serialize(reopened.Document));}});
    Test("purchase date correction follows manual save and survives reopen",()=>{var file=Path.Combine(folder,"purchase-date.mpb");var session=new DocumentSession();session.Apply(PurchaseDateFixture());session.Save(file);var bytes=File.ReadAllBytes(file);session.Apply(WishlistService.EditActivityDate(session.Document,session.Document.WishlistEvents.Last().Id,new(2026,10,12)));Equal(true,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));session.Save(file);var again=new DocumentSession();again.Open(file);Equal(false,again.Dirty);Equal(FinanceDocument.CurrentVersion,again.Document.Version);Equal(new DateOnly(2026,10,12),again.Document.WishlistEvents.Last().Date);Equal(new DateOnly(2026,10,12),again.Document.Transactions.Last().Date);Equal(1000m,again.Document.Transactions.Last().Fee);Equal(WishlistStatus.Purchased,again.Document.WishlistState(wishlistId));});
    Test("historical planned and update dates survive manual save and reopen",()=>{var file=Path.Combine(folder,"wishlist-planned.mpb");var session=new DocumentSession();session.Apply(WishlistEmpty());session.Save(file);var bytes=File.ReadAllBytes(file);session.Apply(WishlistService.Edit(session.Document,Laptop(),new(2026,9,26),new(2026,9,25)));Equal(true,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));session.Save(file);var again=new DocumentSession();again.Open(file);Equal(FinanceDocument.CurrentVersion,again.Document.Version);Equal(false,again.Dirty);Equal(new DateOnly(2026,9,25),again.Document.WishlistEvents.First().Date);Equal(new DateOnly(2026,9,26),again.Document.WishlistEvents.Last().Date);});
    Test("corrected allocation uses manual Save and survives reopen in format six",()=>{
        var file=Path.Combine(folder,"allocation-date.mpb");var session=new DocumentSession();session.Apply(AllocationDateFixture());session.Save(file);var bytes=File.ReadAllBytes(file);
        session.Apply(WishlistService.EditAllocationDate(session.Document,session.Document.WishlistEvents.Last().Id,correctedAllocationDate));Equal(true,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));
        session.Save(file);var reopened=new DocumentSession();reopened.Open(file);Equal(false,reopened.Dirty);Equal(FinanceDocument.CurrentVersion,reopened.Document.Version);Equal(correctedAllocationDate,reopened.Document.WishlistEvents.Last().Date);Equal(135_000m,reopened.Document.WishlistAllocated(wishlistId,bankA.Id,correctedAllocationDate));
    });
    Test("wishlist state and dated allocations survive save and reopen",()=>{
        var file=Path.Combine(folder,"wishlist.mpb");var session=new DocumentSession();session.Apply(WishlistBought());session.Save(file);var reopened=new DocumentSession();reopened.Open(file);
        Equal(WishlistStatus.Purchased,reopened.Document.WishlistState(wishlistId));Equal(100_000m,reopened.Document.WishlistAllocated(wishlistId));Equal(false,reopened.Dirty);Equal(FinanceDocument.CurrentVersion,reopened.Document.Version);
    });
    Test("format five opens without rewriting bytes or inventing wishlist entries",()=>{
        var file=Path.Combine(folder,"format5.mpb");var d=Complete();d.Version=5;File.WriteAllText(file,JsonSerializer.Serialize(d));var bytes=File.ReadAllBytes(file);var session=new DocumentSession();session.Open(file);
        Equal(FinanceDocument.CurrentVersion,session.Document.Version);Equal(0,session.Document.Wishlists.Count);Equal(false,session.Dirty);Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file)));
    });
    Test("saved report selection survives document save and reopen",() => {
        var file = Path.Combine(folder,"saved-period.mpb"); var d = Complete(); d.PaySchedule = Schedule(21,WeekendAdjustment.PreviousWorkingDay); d.SavedPeriod = paySelection;
        var session = new DocumentSession(); session.Apply(d); session.Save(file);
        var reopened = new DocumentSession(); reopened.Open(file); Equal(paySelection,ReportPeriod.Restore(reopened.Document,new(2027,1,1))); Equal(false,reopened.Dirty);
        var copy = reopened.Document.Copy(); copy.SavedPeriod = paySelection with { Mode = ReportPeriodMode.CalendarMonth }; reopened.Apply(copy); Equal(true,reopened.Dirty); reopened.Save(file);
        var again = new DocumentSession(); again.Open(file); Equal(ReportPeriodMode.CalendarMonth,ReportPeriod.Restore(again.Document,new(2027,1,1)).Mode);
    });
    Test("income basis and all fees survive save and reopen",() => { var d = Empty(); d.Transactions = [Income(97_500) with { Fee = 2500, IncomeBasis = IncomeAmountBasis.AfterFee },Expense() with { Amount = 50_000, Fee = 1000 }]; var s = new DocumentSession(); s.Apply(d); var file = Path.Combine(folder,"all-fees.mpb"); s.Save(file); var loaded = new DocumentSession(); loaded.Open(file); Equal(IncomeAmountBasis.AfterFee,loaded.Document.Transactions[0].IncomeBasis); Equal(97_500m,loaded.Document.Transactions[0].Amount); Equal(100_000m,loaded.Document.Totals(start,new(2026,10,31)).Income); Equal(53_500m,loaded.Document.Totals(start,new(2026,10,31)).Expense); Equal(3_046_500m,loaded.Document.Balance(bankA.Id)); });
    Test("format 3 remains unchanged until manual save",() => { var d = Complete(); d.Version = 3; var file = Path.Combine(folder,"format3.mpb"); File.WriteAllText(file,JsonSerializer.Serialize(d)); var bytes = File.ReadAllBytes(file); var s = new DocumentSession(); s.Open(file); Equal(FinanceDocument.CurrentVersion,s.Document.Version); Equal(true,s.Document.Transactions.All(t => t.IncomeBasis == IncomeAmountBasis.BeforeFee)); Equal(9_747_500m,s.Document.Balance(bankA.Id)); Equal(false,s.Dirty); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file))); });
    Test("pay settings survive manual save and reopen",() => { var d = Complete(); d.PaySchedule = Schedule(21,WeekendAdjustment.NextWorkingDay); var s = new DocumentSession(); s.Apply(d); var file = Path.Combine(folder,"pay-cycle.mpb"); s.Save(file); var loaded = new DocumentSession(); loaded.Open(file); Equal(new DateOnly(2026,11,23),loaded.Document.PaySchedule!.Cycle(Nov2026).Start); Equal(1,loaded.Document.PaySchedule.Rules.Count); Equal(false,loaded.Dirty); });
    Test("format 4 opens without altering original bytes or inventing pay settings",() => { var d = Complete(); d.Version = 4; var file = Path.Combine(folder,"format4.mpb"); File.WriteAllText(file,JsonSerializer.Serialize(d)); var bytes = File.ReadAllBytes(file); var s = new DocumentSession(); s.Open(file); Equal(FinanceDocument.CurrentVersion,s.Document.Version); Equal(true,s.Document.PaySchedule is null); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file))); Equal(false,s.Dirty); });
    Test("roundtrip and dirty lifecycle",() => { var s = new DocumentSession(); Equal(false,s.Dirty); s.Apply(Complete()); Equal(true,s.Dirty); s.Save(path); Equal(false,s.Dirty); var loaded = new DocumentSession(); loaded.Open(path); Equal(9_747_500m,loaded.Document.Balance(bankA.Id)); Equal(false,loaded.Dirty); });
    Test("existing file atomically replaced",() => { var s = new DocumentSession(); s.Open(path); var d = s.Document.Copy(); d.Transactions.RemoveAt(0); s.Apply(d); s.Save(path); var loaded = new DocumentSession(); loaded.Open(path); Equal(2,loaded.Document.Transactions.Count); });
    Test("save as switches document path",() => { var s = new DocumentSession(); s.Open(path); var other = Path.Combine(folder,"copy.mpb"); s.Save(other); Equal(other,s.FilePath); Equal(true,File.Exists(path)); });
    Test("failed save keeps changes and original bytes",() => { var s = new DocumentSession(); s.Open(path); var bytes = File.ReadAllBytes(path); s.Apply(Complete()); Reject(() => s.Save(Path.Combine(folder,"missing","x.mpb"))); Equal(true,s.Dirty); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(path))); Equal(path,s.FilePath); });
    Test("locked target failure preserves original and dirty state",() => { var s = new DocumentSession(); s.Open(path); s.Apply(Complete()); var bytes = File.ReadAllBytes(path); using (var locked = new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) Reject(() => s.Save(path)); Equal(true,s.Dirty); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(path))); Equal(0,Directory.GetFiles(folder,"*.tmp").Length); });
    Test("external change is not overwritten",() => { var s = new DocumentSession(); s.Open(path); s.Apply(Complete()); File.AppendAllText(path," "); var bytes = File.ReadAllBytes(path); Reject(() => s.Save(path)); Equal(true,s.Dirty); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(path))); });
    Test("corrupt open preserves unsaved document",() => { var s = new DocumentSession(); s.Apply(Complete()); var corrupt = Path.Combine(folder,"bad.mpb"); File.WriteAllText(corrupt,"not json"); Reject(() => s.Open(corrupt)); Equal(3,s.Document.Transactions.Count); Equal(true,s.Dirty); });
    Test("missing document members rejected",() => { var p = Path.Combine(folder,"empty.mpb"); File.WriteAllText(p,"{}"); Reject(() => new DocumentSession().Open(p)); });
    Test("invalid apply does not replace current document",() => { var s = new DocumentSession(); s.Apply(Complete()); var d = s.Document.Copy(); d.Transactions[0] = Income(0); Reject(() => s.Apply(d)); Equal(8_000_000m,s.Document.Transactions[0].Amount); });
    Test("new clears path data and dirty state",() => { var s = new DocumentSession(); s.Open(path); s.Apply(Complete()); s.New(); Equal<string?>(null,s.FilePath); Equal(0,s.Document.Banks.Count); Equal(false,s.Dirty); });
    FinanceDocument Legacy()
    {
        var d = Complete(); d.Version = 1;
        d.Transactions = d.Transactions.Select(t => t.Kind == TransactionKind.Transfer && t.Fee > 0 ? t with { FeeCategoryId = fees.Id } : t).ToList();
        d.Validate(); return d;
    }
    Test("opening legacy fee categories preserves amounts and the original file",() => {
        var legacy = Legacy(); var legacyPath = Path.Combine(folder,"legacy.mpb"); File.WriteAllText(legacyPath,JsonSerializer.Serialize(legacy));
        var bytes = File.ReadAllBytes(legacyPath); var s = new DocumentSession(); s.Open(legacyPath);
        Equal(FinanceDocument.CurrentVersion,s.Document.Version); Equal<Guid?>(null,s.Document.Transactions.Single(t => t.Kind == TransactionKind.Transfer).FeeCategoryId);
        Equal(legacy.Balance(bankA.Id),s.Document.Balance(bankA.Id)); Equal(legacy.Balance(bankB.Id),s.Document.Balance(bankB.Id));
        Equal(legacy.Categories.Count,s.Document.Categories.Count); Equal(false,s.Dirty); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(legacyPath)));
        var copy = s.Document.Copy(); copy.Categories.RemoveAll(c => c.Id == fees.Id); copy.Validate();
    });
    Test("saving migrated document roundtrips fixed fee with no category dependency",() => {
        var legacyPath = Path.Combine(folder,"legacy-save.mpb"); File.WriteAllText(legacyPath,JsonSerializer.Serialize(Legacy()));
        var s = new DocumentSession(); s.Open(legacyPath); s.Save(legacyPath); var loaded = new DocumentSession(); loaded.Open(legacyPath);
        Equal(FinanceDocument.CurrentVersion,loaded.Document.Version); Equal<Guid?>(null,loaded.Document.Transactions.Single(t => t.Kind == TransactionKind.Transfer).FeeCategoryId);
        Equal(252_500m,loaded.Document.Totals(start,new(2026,10,31)).Expense); Equal(9_747_500m,loaded.Document.Balance(bankA.Id));
    });
    Test("invalid legacy category is rejected before migration",() => {
        var legacy = Legacy(); legacy.Transactions[1] = legacy.Transactions[1] with { FeeCategoryId = salary.Id };
        var legacyPath = Path.Combine(folder,"invalid-legacy.mpb"); File.WriteAllText(legacyPath,JsonSerializer.Serialize(legacy));
        var s = new DocumentSession(); s.Apply(Complete()); Reject(() => s.Open(legacyPath)); Equal(true,s.Dirty); Equal(FinanceDocument.CurrentVersion,s.Document.Version);
    });
    Test("allocated transfer and purpose catalog survive save and reopen",() => { var s = new DocumentSession(); s.Apply(Allocated()); var file = Path.Combine(folder,"allocations.mpb"); s.Save(file); var loaded = new DocumentSession(); loaded.Open(file); Equal(2,loaded.Document.Purposes.Count); Equal(2,loaded.Document.Transactions[1].Allocations!.Count); Equal(800_000m,loaded.Document.Transactions[1].Allocations![0].Amount); Equal(true,loaded.Document.AllocationSummary(loaded.Document.Transactions[1]).Contains("Hutang")); Equal(false,loaded.Dirty); });
    Test("format 2 documents open without inventing purposes or allocations",() => { var d = Complete(); d.Version = 2; var file = Path.Combine(folder,"format2.mpb"); File.WriteAllText(file,JsonSerializer.Serialize(d)); var bytes = File.ReadAllBytes(file); var s = new DocumentSession(); s.Open(file); Equal(FinanceDocument.CurrentVersion,s.Document.Version); Equal(0,s.Document.Purposes.Count); Equal(true,s.Document.Transactions.All(t => t.Allocations is null)); Equal(9_747_500m,s.Document.Balance(bankA.Id)); Equal(true,bytes.SequenceEqual(File.ReadAllBytes(file))); });
}
finally { Directory.Delete(folder,true); }
if (args.Length == 2 && args[0] == "--write-fixture")
{
    var fixture = new DocumentSession(); fixture.Apply(Complete(0)); fixture.Save(args[1]); Console.WriteLine("Fixture: " + args[1]);
}
if (args.Length == 2 && args[0] == "--write-allocation-fixture")
{
    var d = Allocated(); d.Banks[1] = bankB with { Name = "OCBC" };
    d.Transactions[1] = d.Transactions[1] with { Amount = 1_125_662, Allocations = new[] { new TransferAllocation(savings.Id,800_000),new TransferAllocation(debt.Id,325_662) } };
    var fixture = new DocumentSession(); fixture.Apply(d); fixture.Save(args[1]); Console.WriteLine("Allocation fixture: " + args[1]);
}
if(args.Length==2 && args[0]=="--write-wishlist-fixture"){
    var d=WishlistFunded();var headphones=Laptop() with {Id=Guid.NewGuid(),Name="Headphones",TargetAmount=1_500_000};d=WishlistService.Create(d,headphones);d=WishlistService.Allocate(d,headphones.Id,bankA.Id,1_000_000,wishlistDate);var holiday=Laptop() with {Id=Guid.NewGuid(),Name="Holiday",TargetAmount=3_000_000};d=WishlistService.Create(d,holiday);var session=new DocumentSession();session.Apply(d);session.Save(args[1]);Console.WriteLine("Wishlist fixture written.");
}
if(args.Length==2&&args[0]=="--write-purchase-date-fixture"){var session=new DocumentSession();session.Apply(PurchaseDateFixture());session.Save(args[1]);Console.WriteLine("Purchase date fixture written.");}
if(args.Length==2&&args[0]=="--write-wishlist-polish-fixture"){
    var d=BeforeDatePurchase();for(var i=1;i<=24;i++)d=WishlistService.Edit(d,d.Wishlists.Single(),new(2026,10,i));
    d=WishlistService.Purchase(d,wishlistId,bankB.Id,food.Id,172_192,0,new(2026,10,25));d=WishlistService.ReleaseBalance(d,wishlistId,new(2026,10,26));d=WishlistService.Close(d,wishlistId,new(2026,10,27));
    var other=Laptop() with{Id=Guid.NewGuid(),Name="Correction sample",TargetAmount=10_000};d=WishlistService.Create(d,other,new(2026,9,25));d=WishlistService.Allocate(d,other.Id,bankA.Id,10_000,new(2026,9,25));d=WishlistService.Purchase(d,other.Id,bankA.Id,food.Id,9000,0,new(2026,10,11));d=WishlistService.CorrectPurchase(d,other.Id,new(2026,10,12));
    for(var i=1;i<=8;i++)d=WishlistService.Create(d,Laptop() with{Id=Guid.NewGuid(),Name="Plan "+i},new(2026,9,25));
    var session=new DocumentSession();session.Apply(d);session.Save(args[1]);Console.WriteLine("Wishlist polish fixture written.");
}
if(args.Length==2&&args[0]=="--write-self-debt-fixture"){
    var session=new DocumentSession();session.Apply(SelfDebtChecks.Fixture());session.Save(args[1]);Console.WriteLine("Self Debt fixture written.");
}
if(args.Length==2&&args[0]=="--write-borrow-fixture"){
    var session=new DocumentSession();session.Apply(BorrowTransferChecks.Fixture());session.Save(args[1]);Console.WriteLine("Borrow transfer fixture written.");
}
if(args.Length==2&&args[0]=="--write-debt-fixture"){
    var d=Empty();
    foreach(var(name,lender,amount,payment,paid) in new[]{("Personal loan","Alex",1_000_000m,0m,false),("Laptop loan","Sam",500_000m,200_000m,false),("Settlement sample","Alex",300_000m,300_000m,false),("Paid sample","Finance Co",400_000m,400_000m,true)}){
        var sampleDebt=new Debt(Guid.NewGuid(),name,lender,new(2026,10,20));var borrow=Income(amount) with{Date=new(2026,10,4),Fee=1000,DebtId=sampleDebt.Id,Description=name};d=DebtService.SaveTransaction(d,borrow,sampleDebt);
        if(payment>0)d=DebtService.SaveTransaction(d,Expense() with{Date=new(2026,10,20),BankId=bankB.Id,Amount=payment,Fee=2500,DebtId=sampleDebt.Id,Description="Repayment · "+name});
        if(paid)d=DebtService.SetPaid(d,sampleDebt.Id,true);
    }
    d=DebtService.SaveTransaction(d,Income(75_000) with{Date=new(2026,10,2),Description="Historical borrowing not tracked yet"});var session=new DocumentSession();session.Apply(d);session.Save(args[1]);Console.WriteLine("Debt fixture written.");
}
Console.WriteLine($"All {passed} checks passed.");

sealed class UpdateHttpStub(Func<System.Net.Http.HttpRequestMessage,System.Net.Http.HttpResponseMessage> respond) : System.Net.Http.HttpMessageHandler
{
    protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(respond(request));
}

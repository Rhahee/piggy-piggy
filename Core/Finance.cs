using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace MyPiggyBank.Core;

public enum TransactionKind { Income, Expense, Transfer }
public enum CategoryKind { Income, Expense }
public enum IncomeAmountBasis { BeforeFee, AfterFee }
public sealed record Bank(Guid Id, string Name, DateOnly OpeningDate, decimal OpeningBalance);
public sealed record Category(Guid Id, string Name, CategoryKind Kind);
public sealed record Purpose(Guid Id, string Name);
public enum SelfDebtAction { Repay, Borrow }
public sealed record TransferAllocation(Guid PurposeId, decimal Amount, Guid? SelfDebtId = null, SelfDebtAction DebtAction = SelfDebtAction.Repay);
public sealed record Transaction(Guid Id, DateOnly Date, TransactionKind Kind, Guid BankId,
    Guid? ToBankId, Guid? CategoryId, decimal Amount, decimal Fee, Guid? FeeCategoryId, string Description,
    IReadOnlyList<TransferAllocation>? Allocations = null, IncomeAmountBasis IncomeBasis = IncomeAmountBasis.BeforeFee, Guid? DebtId = null, SelfDebtPayment? SelfDebtPayment = null)
{
    [JsonIgnore] public decimal GrossIncome => IncomeBasis == IncomeAmountBasis.AfterFee ? Amount + Fee : Amount;
    [JsonIgnore] public decimal NetIncome => GrossIncome - Fee;
}

public sealed partial class FinanceDocument
{
    public const int CurrentVersion = 9;
    public const string AdminFeeName = "Admin Fee";
    // Guid.Empty cannot be a user category ID; the UI uses it for the system fee filter.
    public static Guid AdminFeeFilterId => Guid.Empty;
    [JsonRequired] public string Format { get; set; } = "MyPiggyBank";
    [JsonRequired] public int Version { get; set; } = CurrentVersion;
    [JsonRequired] public List<Bank> Banks { get; set; } = [];
    [JsonRequired] public List<Category> Categories { get; set; } = [];
    [JsonRequired] public List<Transaction> Transactions { get; set; } = [];
    public List<Purpose> Purposes { get; set; } = [];
    public PaySchedule? PaySchedule { get; set; }
    public ReportPeriod? SavedPeriod { get; set; }

    public FinanceDocument Copy() => new() { Format = Format, Version = Version,
        Banks = [.. Banks], Categories = [.. Categories], Purposes = [.. Purposes], PaySchedule = PaySchedule is null ? null : new(PaySchedule.Rules.ToArray()), SavedPeriod = SavedPeriod, Wishlists = [.. Wishlists], WishlistEvents = [.. WishlistEvents], Debts = [.. Debts],
        Transactions = Transactions.Select(t => t with { Allocations = t.Allocations?.ToArray() }).ToList() };
    public string BankName(Guid id) => Banks.First(b => b.Id == id).Name;
    public string CategoryName(Guid? id) => id is null ? "—" : id == AdminFeeFilterId ? AdminFeeName : Categories.First(c => c.Id == id).Name;
    public string PurposeName(Guid id) => Purposes.First(p => p.Id == id).Name;
    public string AllocationSummary(Transaction item) => item.Allocations is { Count: > 0 }
        ? string.Join("; ", item.Allocations.Select(a => $"{PurposeName(a.PurposeId)}: {Display.Money(a.Amount)}"+(a.SelfDebtId is Guid id?" · "+(a.DebtAction==SelfDebtAction.Borrow?"Borrow: ":"Repay: ")+DebtLabel(id):""))) : "—";
    public void UpgradeToCurrent()
    {
        if (Version == CurrentVersion) return;
        // The legacy category list is retained; only obsolete transfer fee assignments
        // are removed. Principal, fee, dates, descriptions and balances stay intact.
        if (Version == 1) Transactions = Transactions.Select(t => t.Kind == TransactionKind.Transfer ? t with { FeeCategoryId = null } : t).ToList();
        Version = CurrentVersion;
    }
    public decimal Balance(Guid bankId, DateOnly? through = null)
    {
        var bank = Banks.First(b => b.Id == bankId);
        if (through is not null && through < bank.OpeningDate) return 0;
        decimal result = bank.OpeningBalance;
        foreach (var t in Transactions.Where(t => through is null || t.Date <= through))
        {
            if (t.BankId == bankId) result += t.Kind == TransactionKind.Income ? t.NetIncome : -(t.Amount + t.Fee);
            if (t.Kind == TransactionKind.Transfer && t.ToBankId == bankId) result += t.Amount;
        }
        return result;
    }
    public (decimal Income, decimal Expense) Totals(DateOnly start, DateOnly end, Guid? bank = null)
    {
        var items = Transactions.Where(t => t.Date >= start && t.Date <= end && (bank is null || t.BankId == bank));
        return (items.Where(t => t.Kind == TransactionKind.Income).Sum(t => t.GrossIncome),
            items.Where(t => t.Kind == TransactionKind.Expense).Sum(t => t.Amount) + items.Sum(t => t.Fee));
    }
    public bool HasNegativeHistory(Guid bankId)
    {
        var bank = Banks.First(b => b.Id == bankId);
        return bank.OpeningBalance < 0 || Transactions.Select(t => t.Date).Distinct().Any(date => Balance(bankId, date) < 0);
    }
    public static void Require([DoesNotReturnIf(false)] bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    public static bool IsMoney(decimal value) => decimal.Round(value, 2) == value && Math.Abs(value) <= 999_999_999_999_999.99m;
    public void Validate()
    {
        Require(Format == "MyPiggyBank" && Version is 1 or 2 or 3 or 4 or 5 or 6 or 7 or 8 or CurrentVersion, "This file format or version is not supported.");
        Require(Version >= 5 || PaySchedule is null,"Pay-cycle settings require the current document format.");
        PaySchedule?.Validate();
        Require(Version >= 5 || SavedPeriod is null,"Saved report periods require document format 5.");
        SavedPeriod?.Range(PaySchedule);
        Require(Banks is not null && Categories is not null && Transactions is not null, "The document is incomplete.");
        Require(Banks!.All(b => b is not null) && Categories!.All(c => c is not null) && Transactions!.All(t => t is not null), "The document contains invalid entries.");
        Require(Banks.Select(b => b.Id).Distinct().Count() == Banks.Count && Banks.All(b => b.Id != Guid.Empty), "Bank identifiers must be unique.");
        Require(Banks.All(b => !string.IsNullOrWhiteSpace(b.Name) && b.Name == b.Name.Trim() && b.Name.Length <= 100), "Enter a bank name (up to 100 characters).");
        Require(Banks.Select(b => b.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Banks.Count, "A bank with this name already exists.");
        Require(Banks.All(b => b.OpeningDate != default && IsMoney(b.OpeningBalance)), "Enter a valid opening date and balance with at most two decimals.");
        Require(Categories!.Select(c => c.Id).Distinct().Count() == Categories.Count && Categories.All(c => c.Id != Guid.Empty), "Category identifiers must be unique.");
        Require(Categories.All(c => Enum.IsDefined(c.Kind) && !string.IsNullOrWhiteSpace(c.Name) && c.Name == c.Name.Trim() && c.Name.Length <= 100), "Enter a category name and type.");
        Require(Categories.Select(c => $"{c.Kind}:{c.Name}").Distinct(StringComparer.OrdinalIgnoreCase).Count() == Categories.Count, "A category with this name and type already exists.");
        Require(Purposes is not null && Purposes.All(p => p is not null), "The purpose list is invalid.");
        Require(Purposes.Select(p => p.Id).Distinct().Count() == Purposes.Count && Purposes.All(p => p.Id != Guid.Empty), "Purpose identifiers must be unique.");
        Require(Purposes.All(p => !string.IsNullOrWhiteSpace(p.Name) && p.Name == p.Name.Trim() && p.Name.Length <= 100), "Enter a purpose name (up to 100 characters).");
        Require(Purposes.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() == Purposes.Count, "A purpose with this name already exists.");
        Require(Version >= 3 || Purposes.Count == 0, "Purposes require the current document format.");
        Require(Transactions!.Select(t => t.Id).Distinct().Count() == Transactions.Count && Transactions.All(t => t.Id != Guid.Empty), "Transaction identifiers must be unique.");
        foreach (var t in Transactions)
        {
            Require(Enum.IsDefined(t.Kind), "Choose a valid transaction type.");
            var source = Banks.FirstOrDefault(b => b.Id == t.BankId);
            Require(source is not null, "Choose a bank.");
            Require(t.Date != default && t.Date >= source!.OpeningDate, "Transaction date cannot be before the bank's opening balance date.");
            Require(t.Amount > 0 && IsMoney(t.Amount), "Amount must be greater than zero and have at most two decimal places.");
            Require(t.Fee >= 0 && IsMoney(t.Fee), "Admin fee must be zero or greater, with at most two decimals.");
            Require(Enum.IsDefined(t.IncomeBasis), "Choose a valid income amount basis.");
            Require(t.Kind == TransactionKind.Income || t.IncomeBasis == IncomeAmountBasis.BeforeFee, "Amount basis applies to income only.");
            Require(Version >= 4 || (t.IncomeBasis == IncomeAmountBasis.BeforeFee && (t.Kind == TransactionKind.Transfer || t.Fee == 0)), "Income and expense fees require the current document format.");
            Require(t.Description is not null && t.Description.Length <= 2000, "Description must be at most 2,000 characters.");
            if (t.Allocations is { Count: > 0 } allocations)
            {
                Require(Version >= 3 && t.Kind == TransactionKind.Transfer, "Allocations are available for transfers only.");
                Require(allocations.All(a => a is not null && Purposes.Any(p => p.Id == a.PurposeId)), "Choose an existing purpose for each allocation.");
                Require(allocations.All(a => a.Amount > 0 && IsMoney(a.Amount)), "Each allocation amount must be greater than zero, with at most two decimals.");
                Require(allocations.All(a=>Enum.IsDefined(a.DebtAction)&&(a.SelfDebtId is not null||a.DebtAction==SelfDebtAction.Repay)),"Choose a Self Debt for each Borrow allocation.");
                Require(allocations.Select(a => (a.PurposeId,a.SelfDebtId,a.DebtAction)).Distinct().Count() == allocations.Count, "Use each purpose, Self Debt and action combination only once in a transfer.");
                Require(allocations.Sum(a => a.Amount) == t.Amount, "Allocated total must equal the transfer amount, excluding the admin fee.");
            }
            if (t.Kind == TransactionKind.Transfer)
            {
                var target = Banks.FirstOrDefault(b => b.Id == t.ToBankId);
                Require(target is not null && target.Id != t.BankId, "Choose two different banks for a transfer.");
                Require(t.Date >= target!.OpeningDate, "Transfer date cannot be before either bank's opening balance date.");
                Require(t.CategoryId is null, "Transfers do not have an income or expense category.");
                Require(t.Fee >= 0 && IsMoney(t.Fee), "Transfer fee must be zero or greater, with at most two decimals.");
                if (Version == 1)
                    Require(t.Fee == 0 ? t.FeeCategoryId is null : Categories.Any(c => c.Id == t.FeeCategoryId && c.Kind == CategoryKind.Expense), "The legacy transfer fee category is invalid.");
                else
                    Require(t.FeeCategoryId is null, "Transfer fees use the automatic Admin Fee category.");
            }
            else
            {
                Require(t.ToBankId is null && t.FeeCategoryId is null, "Only transfers may have a destination bank; fees use Admin Fee.");
                Require(Categories.Any(c => c.Id == t.CategoryId && (int)c.Kind == (int)t.Kind), "Choose a category matching the transaction type.");
                if (t.Kind == TransactionKind.Income)
                    Require(IsMoney(t.GrossIncome) && t.NetIncome >= 0, "Income before fee must cover the admin fee and stay within the supported amount range.");
            }
        }
        ValidateWishlists();
        ValidateDebts();
    }
}

public static class Display
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");
    public static string Money(decimal value) => (value < 0 ? "−Rp " : "Rp ") + Math.Abs(value).ToString("N2", Culture);
    public static string Date(DateOnly value) => value.ToString("MM/dd/yyyy", Culture);
    public static decimal ParseMoney(string value)
    {
        value = value.Trim();
        if (value.StartsWith("Rp ", StringComparison.OrdinalIgnoreCase)) value = value[3..];
        // Strict grouping avoids silently interpreting an Indonesian decimal comma as thousands.
        FinanceDocument.Require(System.Text.RegularExpressions.Regex.IsMatch(value, @"^-?(?:\d+|\d{1,3}(?:,\d{3})+)(?:\.\d{1,2})?$"), "Use a number such as 1,250,000.00 (comma for thousands, dot for decimals).");
        FinanceDocument.Require(decimal.TryParse(value, NumberStyles.Number, Culture, out var result) && FinanceDocument.IsMoney(result), "Enter a valid amount with at most two decimal places.");
        return result;
    }
}

public sealed class DocumentSession
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public FinanceDocument Document { get; private set; } = new();
    public string? FilePath { get; private set; }
    public bool Dirty { get; private set; }
    private string? savedHash;
    public void New() { Document = new(); FilePath = null; savedHash = null; Dirty = false; }
    public void Apply(FinanceDocument document) { document.Validate(); var normalized = document.Copy(); normalized.UpgradeToCurrent(); normalized.Validate(); Document = normalized; Dirty = true; }
    public void Open(string path)
    {
        FinanceDocument.Require(new FileInfo(path).Length <= 50 * 1024 * 1024, "This document exceeds the 50 MB size limit.");
        var bytes = File.ReadAllBytes(path);
        FinanceDocument.Require(bytes.Length <= 50 * 1024 * 1024, "This document exceeds the 50 MB size limit.");
        var document = JsonSerializer.Deserialize<FinanceDocument>(bytes, Options) ?? throw new InvalidDataException("This file is empty.");
        document.Validate(); // Do not replace the open document until validation succeeds.
        document.UpgradeToCurrent(); document.Validate();
        Document = document; FilePath = Path.GetFullPath(path); savedHash = Hash(bytes); Dirty = false;
    }
    public void Save(string path)
    {
        Document.Validate();
        path = Path.GetFullPath(path);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(Document, Options);
        FinanceDocument.Require(bytes.Length <= 50 * 1024 * 1024, "This document exceeds the 50 MB size limit.");
        var folder = Path.GetDirectoryName(path)!;
        var temporary = Path.Combine(folder, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            { stream.Write(bytes); stream.Flush(true); }
            if (File.Exists(path))
            {
                if (string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
                    FinanceDocument.Require(Hash(File.ReadAllBytes(path)) == savedHash, "This file was changed outside this window. Use Save As to preserve your changes in another file.");
                File.Replace(temporary, path, null); // Atomic replacement; old file survives failed replacement.
            }
            else
            {
                if (string.Equals(path, FilePath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("The original file is missing. Use Save As to choose a new location.");
                File.Move(temporary, path);
            }
            FilePath = path; savedHash = Hash(bytes); Dirty = false;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}

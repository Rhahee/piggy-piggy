using System.Text;
using System.Text.RegularExpressions;

namespace MyPiggyBank.Core;

public readonly record struct MoneyEdit(string Text, int Caret);

public static class MoneyEditing
{
    // Keep incomplete decimals editable. Never round or pad while the user types.
    public static MoneyEdit Format(string text, int caret)
    {
        caret = Math.Clamp(caret, 0, text.Length);
        var rank = text.Take(caret).Count(c => c != ',');
        var raw = text.Replace(",", "");
        FinanceDocument.Require(Regex.IsMatch(raw, @"^-?[0-9]*(?:\.[0-9]{0,2})?$"), "Use digits and a dot for up to two decimal places.");
        if (raw is "" or "-") return new(raw, Math.Min(rank, raw.Length));
        var negative = raw.StartsWith('-');
        var number = negative ? raw[1..] : raw;
        var parts = number.Split('.');
        var whole = parts[0];
        if (whole.Length == 0)
        {
            whole = "0";
            if (rank > (negative ? 1 : 0)) rank++;
        }
        else
        {
            var trimmed = whole.TrimStart('0');
            if (trimmed.Length == 0) trimmed = "0";
            var removed = whole.Length - trimmed.Length;
            rank -= Math.Min(removed, Math.Max(0, rank - (negative ? 1 : 0)));
            whole = trimmed;
        }
        FinanceDocument.Require(whole.Length <= 15, "Use an amount no greater than 999,999,999,999,999.99.");
        var grouped = new StringBuilder();
        if (negative) grouped.Append('-');
        for (var i = 0; i < whole.Length; i++)
        {
            if (i > 0 && (whole.Length - i) % 3 == 0) grouped.Append(',');
            grouped.Append(whole[i]);
        }
        if (parts.Length == 2) grouped.Append('.').Append(parts[1]);
        var formatted = grouped.ToString();
        var target = 0;
        for (var seen = 0; target < formatted.Length && seen < rank; target++)
            if (formatted[target] != ',') seen++;
        return new(formatted, target);
    }

    public static string Paste(string text)
    {
        text = text.Trim();
        if (text.StartsWith("−Rp ", StringComparison.OrdinalIgnoreCase) || text.StartsWith("-Rp ", StringComparison.OrdinalIgnoreCase)) text = "-" + text[4..];
        else if (text.StartsWith("Rp ", StringComparison.OrdinalIgnoreCase)) text = text[3..];
        FinanceDocument.Require(Regex.IsMatch(text, @"^-?(?:(?:[0-9]+|[0-9]{1,3}(?:,[0-9]{3})+)(?:\.[0-9]{0,2})?|\.[0-9]{1,2})$"), "Paste a number such as 1,250,000.00.");
        return Format(text, text.Length).Text;
    }

    public static decimal Read(string text, bool emptyAsZero = false)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            FinanceDocument.Require(emptyAsZero, "Enter an amount.");
            return 0;
        }
        if (text.EndsWith('.')) text = text[..^1];
        return Display.ParseMoney(text);
    }
}

using System.IO;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MyPiggyBank.Core;

namespace MyPiggyBank;

internal static class FormValidation
{
    private static readonly object TargetKey = new();

    // Keep the original exception and domain message; UI targeting stays out of the document model.
    public static T At<T>(FrameworkElement input, Func<T> read)
    {
        try { return read(); }
        catch (Exception error) when (error is InvalidDataException or FormatException or OverflowException)
        {
            if (!error.Data.Contains(TargetKey)) error.Data[TargetKey] = input;
            throw;
        }
    }
    public static void At(FrameworkElement input, Action read) => At(input, () => { read(); return true; });
    public static void Require([DoesNotReturnIf(false)] bool condition, FrameworkElement input, string message)
        => At(input, () => FinanceDocument.Require(condition, message));
    public static decimal Money(TextBox input, bool emptyAsZero = false)
        => At(input, () => MoneyEditing.Read(input.Text, emptyAsZero));
    public static DateOnly Date(TextBox input) => At(input, () =>
    {
        FinanceDocument.Require(DateOnly.TryParseExact(input.Text.Trim(), "MM/dd/yyyy", Display.Culture,
            System.Globalization.DateTimeStyles.None, out var date), "Enter a valid date in MM/DD/YYYY format.");
        return date;
    });
    public static DateOnly? OptionalDate(TextBox input)
    {
        if (string.IsNullOrWhiteSpace(input.Text)) return null;
        var date = Date(input);
        Require(date != default,input,"Enter a valid date or leave it blank.");
        return date;
    }
    public static FrameworkElement? Input(DependencyObject root)
    {
        if (root is TextBoxBase or Selector) return (FrameworkElement)root;
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            if (Input(child) is { } input) return input;
        return null;
    }
    public static void Focus(Exception error)
    {
        if (error.Data[TargetKey] is not FrameworkElement { IsVisible: true, IsEnabled: true } input) return;
        input.BringIntoView(); input.Focus();
        if (input is TextBox text) text.SelectAll();
    }
}

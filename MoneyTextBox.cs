using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed class MoneyTextBox : TextBox
{
    private bool formatting;

    public MoneyTextBox(decimal value = 0)
    {
        Style = (Style)Application.Current.FindResource("MoneyInputStyle");
        TextAlignment = TextAlignment.Right;
        ToolTip = "Type an amount; thousands separators are added automatically.";
        // The watermark belongs to the template, never to Text or the saved value.
        Text = value == 0 ? "" : value.ToString("N2", Display.Culture);
        TextChanged += (_, _) => FormatText();
        DataObject.AddPastingHandler(this, OnPaste);
        LostKeyboardFocus += (_, _) => {
            if (Text.Length == 0) return;
            try { Text = MoneyEditing.Read(Text).ToString("N2", Display.Culture); }
            catch (InvalidDataException) { /* Preserve incomplete input for validation. */ }
        };
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        base.OnPreviewTextInput(e);
        if (e.Handled) return;
        try
        {
            if (e.Text.Contains(',')) { e.Handled = true; return; }
            MoneyEditing.Format(Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, e.Text), SelectionStart + e.Text.Length);
        }
        catch (InvalidDataException) { e.Handled = true; }
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || SelectionLength != 0 || Keyboard.Modifiers != ModifierKeys.None) return;
        // Group separators are visual: deleting one should delete the adjacent digit,
        // rather than immediately reinserting the separator with no apparent effect.
        var position = CaretIndex;
        if (e.Key == Key.Back && position >= 2 && Text[position - 1] == ',')
        {
            Select(position - 2, 2); SelectedText = ""; e.Handled = true;
        }
        else if (e.Key == Key.Delete && position + 1 < Text.Length && Text[position] == ',')
        {
            Select(position, 2); SelectedText = ""; e.Handled = true;
        }
    }

    private void OnPaste(object sender, DataObjectPastingEventArgs e)
    {
        e.CancelCommand();
        if (!e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText)) return;
        try
        {
            var pasted = MoneyEditing.Paste((string)e.SourceDataObject.GetData(DataFormats.UnicodeText)!);
            var combined = Text.Remove(SelectionStart, SelectionLength).Insert(SelectionStart, pasted);
            MoneyEditing.Format(combined, SelectionStart + pasted.Length);
            SelectedText = pasted;
        }
        catch (InvalidDataException) { /* Invalid paste leaves the existing value intact. */ }
    }

    private void FormatText()
    {
        if (formatting) return;
        try
        {
            var result = MoneyEditing.Format(Text, CaretIndex);
            if (result.Text == Text) return;
            formatting = true;
            Text = result.Text;
            CaretIndex = result.Caret;
        }
        catch (InvalidDataException) { /* Programmatic invalid input remains available to validation. */ }
        finally { formatting = false; }
    }
}

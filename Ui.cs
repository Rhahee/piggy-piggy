using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using System.Windows.Automation;
using System.Windows.Controls.Primitives;
using System.Xml.Linq;
using MyPiggyBank.Core;

namespace MyPiggyBank;
public sealed class SelectionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value?.GetType().GetProperty("Name")?.GetValue(value)?.ToString() ?? value?.ToString() ?? "";
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
public sealed class LedgerTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => new TextBlock { Text = value?.ToString() ?? "", TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
public static class Ui
{
    private static readonly Dictionary<string, BitmapImage> BrandImages = [];
    public static ImageSource BrandImage(string path)
    {
        if (!BrandImages.TryGetValue(path, out var image))
        {
            image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri($"pack://application:,,,/{typeof(Ui).Assembly.GetName().Name};component/Assets/{path}", UriKind.Absolute);
            image.EndInit(); image.Freeze(); BrandImages[path] = image;
        }
        return image;
    }
    public static ImageSource DropdownArrow => Drawing("chevron_down");
    public static readonly Brush Accent = Brush("#B51B72"), Ink = Brush("#202231"), Muted = Brush("#626777"), Line = Brush("#E2E3EA"), Pale = Brush("#F7E8F1"), Green = Brush("#16864A"), Red = Brush("#D82D3B");
    public static Brush Brush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
    public static TextBlock Text(string text, double size = 14, Brush? color = null, bool bold = false) => new() { Text = text, FontSize = size, Foreground = color ?? Ink, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    public static void ApplyWindowStyle(Window window) { window.FontFamily = new FontFamily("Segoe UI"); window.FontSize = 14; window.UseLayoutRounding = true; window.Icon = BrandImage("app.png"); TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display); }
    public static StackPanel Stack(Orientation orientation = Orientation.Vertical) => new() { Orientation = orientation };
    public static Border Box(UIElement child, Thickness? padding = null, Brush? background = null) => new() { Child = child, Padding = padding ?? new Thickness(20), Background = background ?? Brushes.White, BorderBrush = Line, BorderThickness = new Thickness(1) };
    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button { Content = text, MinHeight = 32 };
        if (primary) { b.Background = Accent; b.Foreground = Brushes.White; b.BorderBrush = Accent; }
        b.Click += (_, _) => action(); return b;
    }
    private static readonly Dictionary<string, Geometry> Geometries = [];
    public static ImageSource Drawing(string name, Brush? brush = null)
    {
        if (!Geometries.TryGetValue(name, out var geometry))
        {
            var stream = Application.GetResourceStream(new Uri($"/Assets/Icons/{name}.svg", UriKind.Relative))!.Stream;
            using (stream)
            {
                var xml = XDocument.Load(stream);
                var group = new GeometryGroup { FillRule = FillRule.Nonzero };
                foreach (var path in xml.Descendants().Where(e => e.Name.LocalName == "path"))
                    group.Children.Add(Geometry.Parse((string?)path.Attribute("d") ?? ""));
                group.Freeze(); geometry = group; Geometries[name] = geometry;
            }
        }
        var drawing = new DrawingImage(new GeometryDrawing(brush ?? Ink, null, geometry)); drawing.Freeze(); return drawing;
    }
    public static Image Icon(string name, double size = 24, Brush? color = null) => new() { Source = Drawing(name, color), Width = size, Height = size, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
    public static Button IconButton(string icon, string label, Action action, Brush? color = null, bool vertical = true)
    {
        var s = Stack(vertical ? Orientation.Vertical : Orientation.Horizontal);
        var image = Icon(icon, vertical ? 28 : 20, color);
        image.Margin = label.Length == 0 ? new Thickness(0) : vertical ? new Thickness(0,0,0,6) : new Thickness(0,0,8,0);
        s.Children.Add(image);
        var button = Button("", action); button.Content = s; button.Background = Brushes.Transparent; button.BorderThickness = new Thickness(0); button.Padding = new Thickness(12,8,12,8);
        if (label.Length > 0) { var text = Text(label,14); text.HorizontalAlignment = HorizontalAlignment.Center; text.SetBinding(TextBlock.ForegroundProperty,new Binding("Foreground") { Source = button }); s.Children.Add(text); }
        return button;
    }
    public static Button SquareIconButton(string icon, Action action, string tooltip, double size = 32, Brush? color = null)
    {
        var button = IconButton(icon,"",action,color,false); button.Width = button.Height = size; button.MinHeight = 0;
        button.Padding = new Thickness(0); button.Margin = new Thickness(0); button.ToolTip = tooltip; button.VerticalAlignment = VerticalAlignment.Center; return button;
    }
    public static void SelectNavigation(Button button, bool selected)
    {
        button.Style = (Style)Application.Current.FindResource("NavigationButton");
        button.Background = selected ? Accent : Brushes.Transparent; button.Foreground = selected ? Brushes.White : Ink;
        button.Tag = selected;
    }
    public static DataGridTextColumn Column(string label, string path, double width, bool money = false)
    {
        var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(FrameworkElement.ToolTipProperty, new Binding(path) { Converter = new LedgerTooltipConverter() }));
        if (money) style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        return new DataGridTextColumn { Header = label, Binding = new Binding(path), Width = new DataGridLength(width, DataGridLengthUnitType.Star), MinWidth = money ? 160 : 100, ElementStyle = style };
    }
    public static DataGridTemplateColumn TypeColumn()
    {
        return LedgerIconColumn("Type", "Type", 56, imagePath: "TypeImage");
    }
    public static DataGridTemplateColumn AllocationColumn() => LedgerIconColumn("Allocations", "Allocation", 48, "target", "HasAllocation");
    public static DataGridTemplateColumn DebtColumn() => LedgerIconColumn("Debt", "Debt", 48, "money", "HasDebt", tooltipPath: "DebtTooltip");
    private static DataGridTemplateColumn LedgerIconColumn(string label, string path, double width, string? icon = null, string? visiblePath = null, string? imagePath = null, string? tooltipPath = null)
    {
        var cell = new FrameworkElementFactory(typeof(Border));
        cell.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        cell.SetBinding(FrameworkElement.ToolTipProperty, new Binding(tooltipPath ?? path) { Converter = new LedgerTooltipConverter() });
        cell.SetBinding(AutomationProperties.NameProperty, new Binding(path));
        cell.SetValue(ToolTipService.ShowDurationProperty, 60000);
        if (visiblePath is not null)
            cell.SetBinding(UIElement.VisibilityProperty, new Binding(visiblePath) { Converter = new BooleanToVisibilityConverter() });
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(Image.WidthProperty, 20.0); image.SetValue(Image.HeightProperty, 20.0);
        image.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        image.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        image.SetBinding(AutomationProperties.NameProperty, new Binding(path));
        if (imagePath is not null) image.SetBinding(Image.SourceProperty, new Binding(imagePath));
        else image.SetValue(Image.SourceProperty, Drawing(icon!, Accent));
        cell.AppendChild(image);
        var headerStyle = new Style(typeof(DataGridColumnHeader), (Style)Application.Current.FindResource(typeof(DataGridColumnHeader)));
        headerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        headerStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        headerStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, label));
        var cellStyle = new Style(typeof(DataGridCell), (Style)Application.Current.FindResource(typeof(DataGridCell)));
        cellStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        cellStyle.Setters.Add(new Setter(AutomationProperties.NameProperty, new Binding(path)));
        object header = label;
        if (icon is not null)
        {
            var headerIcon = Icon(icon, 16, Muted); headerIcon.ToolTip = label;
            AutomationProperties.SetName(headerIcon, label); header = headerIcon;
        }
        return new DataGridTemplateColumn { Header = header, HeaderStyle = headerStyle, CellStyle = cellStyle,
            CellTemplate = new DataTemplate { VisualTree = cell }, Width = width, MinWidth = width, MaxWidth = width,
            CanUserResize = false, SortMemberPath = path };
    }
}

public sealed class LedgerRow
{
    public required Transaction Item { get; init; }
    public bool IsFee { get; init; }
    public required string Date { get; init; }
    public required string Type { get; init; }
    public required ImageSource TypeImage { get; init; }
    public required string Bank { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public string Allocation { get; init; } = "—";
    public string Debt { get; init; } = "—";
    public bool HasAllocation => !IsFee && Item.Allocations is { Count: > 0 };
    public bool HasDebt => !string.IsNullOrWhiteSpace(Debt) && Debt != "—";
    public string DebtTooltip => IsFee ? Debt + "\nLinked Admin Fee; does not change debt principal." : Debt;
    public required string Amount { get; init; }
    public required decimal NumericAmount { get; init; }
    public required DateOnly SortDate { get; init; }
    public static Func<LedgerRow, bool> Search(string text)
    {
        var term = text.Trim();
        decimal? amount = null;
        try { amount = Display.ParseMoney(term); }
        catch (System.IO.InvalidDataException) { /* Non-numeric terms still search the visible text and linked details. */ }
        return row => (amount is decimal value && row.NumericAmount == value) ||
            ($"{row.Date} {row.Type} {row.Bank} {row.Category} {row.Description} {row.Allocation} {row.Debt} {row.Amount}")
                .Contains(term, StringComparison.OrdinalIgnoreCase);
    }
    public static IEnumerable<LedgerRow> From(FinanceDocument doc, Transaction t)
    {
        var icon = t.Kind == TransactionKind.Income ? "arrow_up" : t.Kind == TransactionKind.Expense ? "arrow_down" : "arrow_swap";
        var brush = t.Kind == TransactionKind.Income ? Ui.Green : t.Kind == TransactionKind.Expense ? Ui.Red : Ui.Ink;
        var principal = t.Kind == TransactionKind.Income ? t.GrossIncome : t.Amount;
        yield return new() { Item = t, Date = Display.Date(t.Date), SortDate = t.Date, Type = t.Kind.ToString(), TypeImage = Ui.Drawing(icon, brush), Bank = doc.BankName(t.BankId) + (t.ToBankId is Guid to ? " → " + doc.BankName(to) : ""), Category = doc.CategoryName(t.CategoryId), Description = t.Description, Allocation = doc.AllocationSummary(t),Debt=doc.TransactionDebtLabel(t), Amount = Display.Money(principal), NumericAmount = principal };
        if (t.Fee > 0) yield return new() { Item = t, IsFee = true, Date = Display.Date(t.Date), SortDate = t.Date, Type = "Expense", TypeImage = Ui.Drawing("arrow_down", Ui.Red), Bank = doc.BankName(t.BankId), Category = FinanceDocument.AdminFeeName, Description = t.Kind + " fee" + (t.Description.Length > 0 ? " · " + t.Description : ""),Debt=!doc.RelatedDebtIds(t).Any()?"—":doc.TransactionDebtLabel(t)+" (Admin Fee)", Amount = Display.Money(t.Fee), NumericAmount = t.Fee };
    }
}

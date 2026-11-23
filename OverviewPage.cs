using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using MyPiggyBank.Core;

namespace MyPiggyBank;

public sealed partial class MainWindow
{
    private const double SummaryInset = 20;

    private UIElement Overview(WorkspaceTheme theme)
    {
        var outer = new DockPanel { Background = theme.Background };
        var header = PageHeader("Overview", theme);
        DockPanel.SetDock(header, Dock.Top);
        outer.Children.Add(header);

        var content = Ui.Stack();
        content.Margin = new Thickness(24, 12, 24, 24);
        if (session.Document.Banks.Count == 0)
        {
            var empty = Ui.Stack();
            empty.Children.Add(Ui.Text("Add your first bank", 24, theme.Text, true));
            var help = Ui.Text("Set an opening date and balance, then start recording transactions.", 15, theme.Muted);
            help.Margin = new Thickness(0, 8, 0, 16);
            empty.Children.Add(help);
            var button = Ui.Button("Add Bank", AddFirstBank);
            theme.StyleButton(button, primary: true);
            button.HorizontalAlignment = HorizontalAlignment.Left;
            button.Margin = new Thickness(0);
            empty.Children.Add(button);
            content.Children.Add(theme.Card(empty, new Thickness(24)));
        }
        else
        {
            var cardPadding = new Thickness(0, SummaryInset, 0, SummaryInset);
            content.Children.Add(theme.Card(OverviewBalance(theme), cardPadding));
            content.Children.Add(OverviewTransactions(theme));
        }
        outer.Children.Add(new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return outer;
    }

    private UIElement OverviewBalance(WorkspaceTheme theme)
    {
        var balance = Ui.Stack();
        Grid.SetIsSharedSizeScope(balance, true);
        var title = selectedBank is Guid bankId ? session.Document.BankName(bankId) + " Balance" : "Total Balance";
        var today = DateOnly.FromDateTime(DateTime.Today);
        var banks = session.Document.Banks.Where(bank => selectedBank is null || bank.Id == selectedBank)
            .Select(bank => (bank.Name, Balance: session.Document.Balance(bank.Id, today),
                Reserved: session.Document.WishlistReserved(bank.Id, today))).ToList();
        var total = banks.Sum(bank => bank.Balance);
        var reserved = banks.Sum(bank => bank.Reserved);

        var heading = Ui.Text(title, 18, theme.Text, true);
        heading.Margin = new Thickness(SummaryInset, 0, SummaryInset, 0);
        balance.Children.Add(heading);
        var value = Ui.Text(Display.Money(total), 36, total < 0 ? theme.Error : theme.Accent, true);
        value.Margin = new Thickness(SummaryInset, 2, SummaryInset, 0);
        balance.Children.Add(value);
        var asOf = Ui.Text("Current balance as of " + Display.Date(today), 14, theme.Muted);
        asOf.Margin = heading.Margin;
        balance.Children.Add(asOf);
        balance.Children.Add(OverviewAmount(theme, "Wishlist Allocation", reserved, theme.Text));
        balance.Children.Add(OverviewAmount(theme, "Available Balance", total - reserved, theme.Text, highlighted: true));
        foreach (var bank in banks)
        {
            var row = OverviewAmount(theme, bank.Name, bank.Balance, theme.Text);
            row.Padding = new Thickness(SummaryInset + 8, 3, SummaryInset + 12, 3);
            row.Margin = new Thickness(0);
            balance.Children.Add(row);
        }

        var panel = new Grid();
        // Reuse the Rahee Design System C ornament as a decorative stroke.
        panel.Children.Add(new Path
        {
            Data = (Geometry)Application.Current.FindResource("OverviewOrnament"), Stroke = theme.Accent,
            StrokeThickness = 1, Opacity = .25, Stretch = Stretch.Uniform, Width = 126, Height = 38,
            HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, SummaryInset, 0), IsHitTestVisible = false
        });
        panel.Children.Add(balance);
        return panel;
    }

    private UIElement OverviewRibbonSummary(WorkspaceTheme theme)
    {
        var summary = new UniformGrid { Rows = 1 };
        var range = SelectedRange;
        var totals = session.Document.Totals(range.Start, range.End, selectedBank);
        foreach (var (label, amount, color) in new[]
        {
            ("Income", totals.Income, theme.Success),
            ("Expenses", totals.Expense, theme.Error),
            ("Net Income", totals.Income - totals.Expense, theme.Text)
        })
        {
            var metric = Ui.Stack();
            metric.VerticalAlignment = VerticalAlignment.Center;
            var caption = Ui.Text(label, 13, theme.Muted);
            var value = Ui.Text(Display.Money(amount), 15, color, true);
            caption.TextAlignment = value.TextAlignment = TextAlignment.Center;
            metric.Children.Add(caption);
            metric.Children.Add(value);
            summary.Children.Add(new Border
            {
                Child = metric, MinWidth = 144, Padding = new Thickness(12, 4, 12, 4),
                Background = label == "Net Income" ? theme.Wash : Brushes.Transparent,
                BorderBrush = theme.Outline, BorderThickness = new Thickness(1, 0, 0, 0)
            });
        }
        return summary;
    }

    private static Border OverviewAmount(WorkspaceTheme theme, string label, decimal amount, Brush valueColor, bool highlighted = false)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Currency" });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, SharedSizeGroup = "Amount" });
        var formatted = Display.Money(amount);
        var separator = formatted.IndexOf(' ');
        var currency = Ui.Text(formatted[..separator], 14, valueColor, true);
        currency.Margin = new Thickness(8, 0, 0, 0);
        currency.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(currency, 1);
        var figure = Ui.Text(formatted[(separator + 1)..], 14, valueColor, true);
        figure.Margin = new Thickness(8, 0, 0, 0);
        figure.TextAlignment = TextAlignment.Right;
        Grid.SetColumn(figure, 2);
        row.Children.Add(Ui.Text(label, 14, highlighted ? theme.Text : theme.Muted, highlighted));
        row.Children.Add(currency);
        row.Children.Add(figure);
        return new Border
        {
            Child = row,
            Background = highlighted ? theme.Wash : Brushes.Transparent,
            Padding = new Thickness(SummaryInset + 8, 7, SummaryInset + 12, highlighted ? 7 : 0),
            Margin = new Thickness(0, 6, 0, 0),
            BorderBrush = theme.Rule,
            BorderThickness = new Thickness(0, highlighted ? 0 : 1, 0, 0)
        };
    }

    private Border OverviewTransactions(WorkspaceTheme theme)
    {
        var recent = Ui.Stack();
        var title = Ui.Text("Recent Transactions", 18, theme.Text, true);
        title.Margin = new Thickness(0, 0, 0, 8);
        recent.Children.Add(title);

        var rows = Rows(false).Take(5).ToList();
        if (rows.Count == 0)
        {
            recent.Children.Add(Ui.Text("No transactions in this period", 14, theme.Text, true));
            recent.Children.Add(Ui.Text("Choose another period or add an Income, Expense, or Transfer.", 13, theme.Muted));
        }
        foreach (var row in rows)
        {
            var typeColor = row.Type switch
            {
                "Income" => theme.Success,
                "Expense" => theme.Error,
                _ => theme.Accent
            };
            var hasDescription = !string.IsNullOrWhiteSpace(row.Description);
            var description = Ui.Text(hasDescription ? row.Description : row.Type, 14,
                hasDescription ? theme.Text : typeColor, true);
            description.TextWrapping = TextWrapping.NoWrap;
            description.TextTrimming = TextTrimming.CharacterEllipsis;
            description.ToolTip = description.Text;

            var metadata = Ui.Text("", 13, theme.Muted);
            metadata.TextWrapping = TextWrapping.NoWrap;
            metadata.TextTrimming = TextTrimming.CharacterEllipsis;
            var facts = row.Date + "  ·  " + row.Bank;
            var typePrefix = hasDescription ? row.Type + "  ·  " : "";
            if (hasDescription)
                metadata.Inlines.Add(new Run(typePrefix) { Foreground = typeColor });
            metadata.Inlines.Add(new Run(facts));
            metadata.ToolTip = typePrefix + facts;

            var text = Ui.Stack();
            text.Children.Add(description);
            text.Children.Add(metadata);
            var entry = new DockPanel();
            var amount = Ui.Text(row.Amount, 14, theme.Text, true);
            amount.Margin = new Thickness(12, 0, 0, 0);
            amount.TextAlignment = TextAlignment.Right;
            DockPanel.SetDock(amount, Dock.Right);
            entry.Children.Add(amount);
            entry.Children.Add(text);

            recent.Children.Add(new Border
            {
                Child = entry, Padding = new Thickness(0, 6, 0, 6),
                BorderBrush = theme.Rule, BorderThickness = new Thickness(0, 1, 0, 0)
            });
        }

        var card = theme.Card(recent, new Thickness(16, 12, 16, 12));
        card.Margin = new Thickness(0, 16, 0, 0);
        return card;
    }
}

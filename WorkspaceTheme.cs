using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;

namespace MyPiggyBank;

internal sealed class WorkspaceTheme
{
    public static readonly WorkspaceTheme Light = new(
        "#F7F5F0", "#FDFCF9", "#EEEBE6", "#292936", "#575766", "#535B9F", "#FFFFFF",
        "#EBECF6", "#DFDDE2", "#77798B", "#205B42", "#93352F");
    public static readonly WorkspaceTheme Dark = new(
        "#191A23", "#252632", "#20212B", "#EFEDF4", "#BCBCCB", "#ADB5F1", "#202342",
        "#303450", "#3E4052", "#A2A3B3", "#B0DFBF", "#FFC0B7");

    public static readonly FontFamily Typeface = new(
        new Uri($"pack://application:,,,/{typeof(WorkspaceTheme).Assembly.GetName().Name};component/Assets/Fonts/"),
        "./#Source Sans 3");

    public Brush Background { get; }
    public Brush Surface { get; }
    public Brush Sidebar { get; }
    public Brush Text { get; }
    public Brush Muted { get; }
    public Brush Accent { get; }
    public Brush OnAccent { get; }
    public Brush Wash { get; }
    public Brush Rule { get; }
    public Brush Outline { get; }
    public Brush Success { get; }
    public Brush Error { get; }

    private WorkspaceTheme(string background, string surface, string sidebar, string text, string muted,
        string accent, string onAccent, string wash, string rule, string outline, string success, string error)
    {
        Background = Ui.Brush(background);
        Surface = Ui.Brush(surface);
        Sidebar = Ui.Brush(sidebar);
        Text = Ui.Brush(text);
        Muted = Ui.Brush(muted);
        Accent = Ui.Brush(accent);
        OnAccent = Ui.Brush(onAccent);
        Wash = Ui.Brush(wash);
        Rule = Ui.Brush(rule);
        Outline = Ui.Brush(outline);
        Success = Ui.Brush(success);
        Error = Ui.Brush(error);
    }

    public Border Card(UIElement child, Thickness padding) => new()
    {
        Child = child, Padding = padding, Background = Surface, BorderBrush = Rule,
        BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(12)
    };

    public GridSplitter Splitter() => new()
    {
        Style = (Style)Application.Current.FindResource("WorkspaceSplitter"),
        Background = Rule
    };

    public TextBlock Label(string text, double size = 14, Brush? color = null, bool bold = false) =>
        Ui.Text(text, size, color ?? Text, bold);

    public Button ActionButton(string text, Action action, bool primary = false)
    {
        var button = Ui.Button(text, action);
        StyleButton(button, primary: primary, outlined: !primary);
        return button;
    }

    public Border SectionHeader(string title, string actionLabel, Action action, string? subtitle = null, Button? trailingAction = null)
    {
        var row = new DockPanel { Margin = new Thickness(24, 2, 24, 2) };
        var button = ActionButton(actionLabel, action, primary: true);
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        actions.Children.Add(button);
        if (trailingAction is not null) actions.Children.Add(trailingAction);
        DockPanel.SetDock(actions, Dock.Right);
        row.Children.Add(actions);
        var heading = Label(title, 20, bold: true);
        if (subtitle is not null)
        {
            DockPanel.SetDock(heading, Dock.Left);
            row.Children.Add(heading);
            var detail = Label(subtitle, 14, Muted);
            detail.Margin = new Thickness(12, 0, 0, 0);
            row.Children.Add(detail);
        }
        else row.Children.Add(heading);
        return new Border { Child = row, BorderBrush = Rule, BorderThickness = new Thickness(0, 0, 0, 1) };
    }

    public void StyleButton(Button button, bool selected = false, bool primary = false, bool outlined = false, bool square = false)
    {
        button.Style = (Style)Application.Current.FindResource("WorkspaceButton");
        button.Background = primary ? Accent : selected ? Wash : Brushes.Transparent;
        button.Foreground = primary ? OnAccent : selected || outlined ? Accent : Text;
        button.BorderBrush = outlined ? Outline : Accent;
        button.BorderThickness = new Thickness(outlined ? 1 : 0);
        button.FontWeight = selected || primary ? FontWeights.SemiBold : FontWeights.Normal;
        if (square) button.Tag = "Square";
    }

    public static WorkspaceTheme For(Window owner) => owner is MainWindow main ? main.Theme
        : owner.TryFindResource(typeof(WorkspaceTheme)) as WorkspaceTheme ?? Light;

    public static WorkspaceTheme ApplyDialog(Window dialog, Window owner)
    {
        var theme = For(owner);
        Ui.ApplyWindowStyle(dialog);
        dialog.FontFamily = Typeface;
        dialog.Foreground = theme.Text;
        dialog.Background = theme.Background;
        theme.ApplyResources(dialog);
        if (!SystemParameters.HighContrast)
        {
            dialog.Resources[typeof(TextBox)] = Application.Current.FindResource("WorkspaceDialogTextBox");
            dialog.Resources[typeof(CheckBox)] = Application.Current.FindResource("WorkspaceDialogCheckBox");
        }
        return theme;
    }

    public DataGrid DialogGrid() => new()
    {
        Background = Surface, RowBackground = Surface, AlternatingRowBackground = Surface,
        Foreground = Text, BorderBrush = Rule, HorizontalGridLinesBrush = Rule
    };

    public void ApplyTo(DockPanel layout)
    {
        layout.Background = Sidebar;
        TextElement.SetFontFamily(layout, Typeface);
        TextElement.SetForeground(layout, Text);
        ApplyResources(layout);
    }

    public void ApplyResources(FrameworkElement element)
    {
        var resources = element.Resources;
        resources[typeof(WorkspaceTheme)] = this;
        resources["WorkspaceInk"] = Text;
        resources["WorkspaceMuted"] = Muted;
        resources["WorkspaceOnAccent"] = OnAccent;
        resources["WorkspaceSurface"] = Surface;
        resources["WorkspaceOutline"] = Outline;
        resources["WorkspaceFocus"] = Accent;
        resources["WorkspaceSelect"] = Wash;
        resources["WorkspaceLedgerHover"] = Wash;
        resources["WorkspaceLedgerSelection"] = Wash;
        resources["WorkspaceLedgerHeader"] = Wash;
        resources["WorkspaceLedgerHeaderInk"] = Text;
        resources["WorkspaceLedgerRule"] = Rule;
        resources["WorkspaceScrollThumb"] = Outline;
        if (!SystemParameters.HighContrast)
            resources[typeof(ScrollBar)] = Application.Current.FindResource("WorkspaceScrollbar");
    }
}

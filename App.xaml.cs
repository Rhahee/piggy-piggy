using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace MyPiggyBank;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en-US");
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement), new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("en-US")));
        DispatcherUnhandledException += (_, args) => { MessageBox.Show(args.Exception.Message, "Piggy", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        if (e.Args.Length == 2 && e.Args[0] == "--apply-update")
        {
            PortableUpdater.RunHelper(e.Args[1]); Shutdown(); return;
        }
        base.OnStartup(e);
        var window = new MainWindow(); window.Show();
        if (e.Args.Length == 1) window.OpenPath(e.Args[0]);
    }
}

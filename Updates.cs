using System.ComponentModel;
using System.Net.Http;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using MyPiggyBank.Core;

namespace MyPiggyBank;

internal sealed class UpdateSession : IDisposable
{
    public static readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version!;
    public static readonly int Epoch = int.Parse(Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "UpdateEpoch").Value!);
    private readonly HttpClient client;
    private readonly CancellationTokenSource lifetime = new();
    public event Action? Changed;
    public UpdateCheck? Result { get; private set; }
    public bool Checking { get; private set; }
    public DateTime? LastChecked { get; private set; }

    public UpdateSession() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 1024 * 1024 }) { }
    internal UpdateSession(HttpClient client) => this.client = client;

    public async Task ActivateAsync(Window owner)
    {
        if (Checking) return;
        Checking = true; Changed?.Invoke();
        try
        {
            Result = await new ReleaseChecker(client).CheckChannelAsync(Version,Epoch,lifetime.Token);
            LastChecked = DateTime.Now;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return; }
        finally { Checking = false; Changed?.Invoke(); }
        if (!owner.IsLoaded) return;
        if (Result is { Status: UpdateStatus.Available, Release: not null }) ShowRelease(owner,Result.Release);
        else ConfirmDialog.Alert(owner,"Software Update",Result!.Message);
    }

    private static void ShowRelease(Window owner, ReleaseInfo release)
    {
        var dialog = new Window { Owner = owner, Title = "Update Available — Piggy", Width = 560, Height = 500,
            ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        Ui.ApplyWindowStyle(dialog);
        var panel = Ui.Stack(); panel.Margin = new Thickness(20); dialog.Content = panel;
        panel.Children.Add(Ui.Text(release.IsVersionTransition ? "Version Transition" : "Update Available",24,bold:true));
        var version = Ui.Text($"Installed: v{AppVersion.Format(Version)}    Available: v{AppVersion.Format(release.Version)}",14,Ui.Muted);
        version.Margin = new Thickness(0,12,0,12); panel.Children.Add(version);
        panel.Children.Add(new TextBox { Text = string.IsNullOrWhiteSpace(release.Notes) ? "No release notes provided." : release.Notes,
            IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        var status = Ui.Text("Download, verify and restart automatically. Your .mpb documents stay in place.",14,Ui.Muted);
        if (release.IsVersionTransition) status.Text = "The version numbering is changing. Your features and .mpb documents are retained. Download, verify and restart automatically.";
        status.Margin = new Thickness(0,12,0,12); panel.Children.Add(status);
        var buttons = Ui.Stack(Orientation.Horizontal); buttons.HorizontalAlignment = HorizontalAlignment.Right;
        var cancel = Ui.Button("Cancel",dialog.Close); var install = Ui.Button("Update and Restart",()=>{},true);
        if (release.Checksum is null) { install.IsEnabled = false; status.Text = "The update package is not ready for automatic installation yet. Please check again later."; }
        install.Margin = new Thickness(8,0,0,0); buttons.Children.Add(cancel); buttons.Children.Add(install); panel.Children.Add(buttons);
        var cancellation = new CancellationTokenSource();
        var busy = false;
        dialog.Closing += (_,_) => cancellation.Cancel();
        install.Click += async (_,_) =>
        {
            if (busy) return; busy = true; install.IsEnabled = false;
            StagedUpdate? package = null;
            try
            {
                package = await PortableUpdater.DownloadAsync(release,new Progress<string>(text => status.Text = text),cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                dialog.Close();
                if (owner is not MainWindow main || !await main.InstallUpdateAsync(package)) PortableUpdater.Cleanup(package.Directory);
            }
            catch (OperationCanceledException)
            {
                if (package is not null) PortableUpdater.Cleanup(package.Directory);
                if (!cancellation.IsCancellationRequested && dialog.IsLoaded)
                { status.Text = "The download timed out. Please try again."; install.IsEnabled = true; busy = false; }
            }
            catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException or Win32Exception or System.Text.Json.JsonException)
            {
                if (package is not null) PortableUpdater.Cleanup(package.Directory);
                if (dialog.IsLoaded) { status.Text = "Update failed: " + e.Message; install.IsEnabled = true; busy = false; }
                else if (owner.IsLoaded) ConfirmDialog.Alert(owner,"Could not update",e.Message + "\n\nThe existing application and your documents have been preserved.");
            }
            finally { if (!dialog.IsLoaded) cancellation.Dispose(); }
        };
        dialog.ShowDialog();
        if (!busy) cancellation.Dispose();
    }
    public void Dispose() { lifetime.Cancel(); client.Dispose(); lifetime.Dispose(); }
}

internal sealed class UpdateIndicator : StackPanel
{
    private readonly Button button;
    private readonly Image icon;
    private readonly Border badge;
    private readonly UpdateSession session;

    public UpdateIndicator(Window owner, UpdateSession session)
    {
        this.session = session; Orientation = Orientation.Horizontal; VerticalAlignment = VerticalAlignment.Center;
        var version = Ui.Text("v" + AppVersion.Format(UpdateSession.Version),12,Ui.Muted); Children.Add(version);
        Children.Add(new Border { Width = 1, Height = 12, Background = Ui.Line, Margin = new Thickness(12,0,12,0) });
        var grid = new Grid { Width = 36, Height = 36 };
        button = Ui.SquareIconButton("arrow_sync",()=>{},"Check for updates",32);
        button.HorizontalAlignment = HorizontalAlignment.Left; button.VerticalAlignment = VerticalAlignment.Bottom;
        button.Style = (Style)Application.Current.FindResource("UpdateIconButton"); button.BorderThickness = new Thickness(1);
        icon = Ui.Icon("arrow_sync",20); button.Content = icon;
        button.Click += async (_,_) => await session.ActivateAsync(owner);
        grid.Children.Add(button);
        badge = new Border { Width = 14, Height = 14, Background = Ui.Accent, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false, Child = new TextBlock { Text = "!", Foreground = Brushes.White,
                FontSize = 11, FontWeight = FontWeights.Bold, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        grid.Children.Add(badge); Children.Add(grid);
        Loaded += (_,_) => { session.Changed += Refresh; Refresh(); };
        Unloaded += (_,_) => session.Changed -= Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var current = session.Result?.Status;
        var color = current == UpdateStatus.UpToDate ? Ui.Green : current == UpdateStatus.Available ? Ui.Accent : Ui.Muted;
        icon.Source = Ui.Drawing("arrow_sync",color);
        button.Background = current == UpdateStatus.UpToDate ? Ui.Brush("#F2FBF5") : Brushes.White;
        button.BorderBrush = current == UpdateStatus.UpToDate ? Ui.Green : Ui.Line;
        badge.Visibility = !session.Checking && current == UpdateStatus.Available ? Visibility.Visible : Visibility.Collapsed;
        if (session.Checking) { icon.Source = Ui.Drawing("arrow_sync",Ui.Muted); button.Background = Brushes.White; button.BorderBrush = Ui.Line; }
        button.IsEnabled = !session.Checking;
        var hint = session.Checking ? "Checking for updates…" : current switch {
            UpdateStatus.UpToDate => "Up to date — click to check again",
            UpdateStatus.Available => $"Update available: v{AppVersion.Format(session.Result!.Release!.Version)} — update and restart",
            UpdateStatus.NoRelease => "No published release yet — click to check again",
            UpdateStatus.Unavailable => "Could not check updates — click to retry",
            _ => "Check for updates" };
        button.ToolTip = hint + (session.LastChecked is DateTime date ? "\nLast checked: " + date.ToString("MM/dd/yyyy HH:mm") : "");
        AutomationProperties.SetName(button,hint);
    }
}

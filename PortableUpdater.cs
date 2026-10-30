using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using MyPiggyBank.Core;

namespace MyPiggyBank;

internal sealed record UpdateJob(int ParentId, long ParentStarted, string Target, StagedUpdate Package, string? Document);
internal static class PortableUpdater
{
    public static async Task<StagedUpdate> DownloadAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken cancellation)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PiggyPiggy/" + AppVersion.Format(UpdateSession.Version));
        var package = await new UpdateDownloader(client).DownloadAsync(release,progress,cancellation);
        try
        {
            var info = FileVersionInfo.GetVersionInfo(package.Executable);
            if (new Version(info.FileMajorPart,info.FileMinorPart,info.FileBuildPart,info.FilePrivatePart) != AppVersion.Normalize(release.Version))
                throw new InvalidDataException("The downloaded application version does not match the release.");
            return package;
        }
        catch { Cleanup(package.Directory); throw; }
    }
    public static async Task StartAsync(StagedUpdate package, string? document)
    {
        var target = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate the running application.");
        // An empty assembly location is deliberately used to distinguish the single-file portable build.
#pragma warning disable IL3000
        if (!target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase) || Path.GetFileName(target).Equals("dotnet.exe",StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(System.Reflection.Assembly.GetExecutingAssembly().Location))
            throw new InvalidOperationException("Automatic updates require the portable EXE build.");
#pragma warning restore IL3000
        // Test directory permissions before closing; never request elevation.
        var probe = Path.Combine(Path.GetDirectoryName(target)!,Guid.NewGuid().ToString("N") + ".tmp");
        using (File.Create(probe)) { } File.Delete(probe);
        using var parent = Process.GetCurrentProcess();
        var job = new UpdateJob(parent.Id,parent.StartTime.ToUniversalTime().Ticks,target,package,document);
        var manifest = Path.Combine(package.Directory,"job.json");
        File.WriteAllText(manifest,JsonSerializer.Serialize(job));
        var helper = Path.Combine(package.Directory,"updater.exe"); File.Copy(target,helper,false);
        var start = new ProcessStartInfo(helper) { UseShellExecute = false, WorkingDirectory = package.Directory };
        start.ArgumentList.Add("--apply-update"); start.ArgumentList.Add(manifest);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the update helper.");
        var ready = Path.Combine(package.Directory,"ready");
        for (var attempt = 0; attempt < 300; attempt++)
        {
            if (File.Exists(ready)) return;
            if (process.HasExited) throw new IOException("The update helper could not prepare the update.");
            await Task.Delay(100);
        }
        process.Kill();
        throw new IOException("The update helper did not become ready. Your app remains open.");
    }
    public static void RunHelper(string manifest)
    {
        UpdateJob? job = null;
        var safeToClean = false;
        try
        {
            job = JsonSerializer.Deserialize<UpdateJob>(File.ReadAllText(manifest)) ?? throw new InvalidDataException("Invalid update job.");
            var root = Path.GetFullPath(job.Package.Directory);
            if (!Path.GetDirectoryName(root)!.Equals(Path.TrimEndingDirectorySeparator(Path.GetTempPath()),StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFileName(root).StartsWith("MyPiggyBank-update-",StringComparison.Ordinal) ||
                !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.Combine(root,"updater.exe"),StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(manifest).Equals(Path.Combine(root,"job.json"),StringComparison.OrdinalIgnoreCase) ||
                !Path.GetFullPath(job.Package.Executable).Equals(Path.Combine(job.Package.Directory,"update.exe"),StringComparison.OrdinalIgnoreCase) ||
                !Path.IsPathFullyQualified(job.Target) || !job.Target.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid update paths.");
            safeToClean = true;
            if (!UpdatePackage.Hash(job.Package.Executable).Equals(job.Package.Sha256,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The staged update has changed.");
            File.WriteAllText(Path.Combine(job.Package.Directory,"ready"),"");
            try
            {
                using var parent = Process.GetProcessById(job.ParentId);
                if (parent.StartTime.ToUniversalTime().Ticks == job.ParentStarted && !parent.WaitForExit(120000))
                    throw new IOException("The app did not close. Update cancelled.");
            }
            catch (ArgumentException) { /* Parent already exited. */ }
            UpdatePackage.Replace(job.Package.Executable,job.Target,job.Package.Sha256,() => Launch(job.Target,job.Document));
        }
        catch (Exception e)
        {
            MessageBox.Show("The update could not be installed. Your previous application and documents are preserved.\n\n" + e.Message,"Piggy Update",MessageBoxButton.OK,MessageBoxImage.Error);
            // On failure leave the old app available; do not repeatedly relaunch an unusable path.
        }
        finally { if (safeToClean && job is not null) Cleanup(job.Package.Directory,keepHelper:true); }
    }
    private static void Launch(string target,string? document)
    {
        var start = new ProcessStartInfo(target) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(target)! };
        if (document is not null && File.Exists(document)) start.ArgumentList.Add(document);
        using var process = Process.Start(start) ?? throw new IOException("Could not restart the application.");
    }
    public static void Cleanup(string directory,bool keepHelper = false) => UpdatePackage.Cleanup(directory,keepHelper);
}

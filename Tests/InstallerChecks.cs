using MyPiggyBank.Core;
using System.Net;
using System.Text.Json;
using System.IO.Compression;
using Microsoft.Win32;

internal static class InstallerChecks
{
    private static void Require(bool condition) { if (!condition) throw new Exception("Installer assertion failed."); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException or JsonException or IOException) { return; }
        throw new Exception("Expected installer rejection.");
    }
    private static ReleaseInfo Resolve(bool checksum = true, bool draft = false, bool offline = false, string version = "0.1.0.2")
    {
        var tag = "app-v" + version;
        var name = $"Piggy-Piggy-{version}-win-x64.zip";
        var url = $"https://github.com/{ReleaseChecker.Repository}/releases/download/{tag}/{name}";
        using var client = new HttpClient(new Handler(request =>
        {
            if (offline) throw new HttpRequestException("offline fixture");
            Require(request.Headers.Authorization is null && request.Content is null);
            if (request.RequestUri == ReleaseChecker.ChannelEndpoint)
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { schema = 1, epoch = 3, version, tag })) };
            Require(request.RequestUri!.AbsoluteUri == $"https://api.github.com/repos/{ReleaseChecker.Repository}/releases/tags/{tag}");
            var assets = new List<object> { new { name, size = 1234, browser_download_url = url } };
            if (checksum) assets.Add(new { name = name + ".sha256", size = 100, browser_download_url = url + ".sha256" });
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { tag_name = tag, draft, prerelease = false, assets })) };
        }));
        return InstallerPackage.ResolveAsync(client).GetAwaiter().GetResult();
    }
    public static void Run(Action<string, Action> test)
    {
        test("web setup resolves the explicit stable channel without GitHub latest", () => Require(Resolve().Version == new Version(0,1,0,2)));
        test("same web setup logic follows a newly promoted channel without rebuilding", () => Require(Resolve(version:"0.1.0.3").Version == new Version(0,1,0,3)));
        test("web setup rejects releases without checksum", () => Reject(() => Resolve(checksum:false)));
        test("web setup rejects unpublished releases", () => Reject(() => Resolve(draft:true)));
        test("web setup fails safely when metadata is offline", () => Reject(() => Resolve(offline:true)));
        var dir = Path.Combine(Path.GetTempPath(), "Piggy-installer-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var zip = Path.Combine(dir,"package.zip");
            using (var archive = ZipFile.Open(zip, ZipArchiveMode.Create))
            {
                // Real versioned PE fixture, not the production application.
                archive.CreateEntryFromFile(typeof(InstallerChecks).Assembly.Location, "Piggy - Piggy.exe");
                foreach (var name in new[] {"README.md","Fluent-Icons-LICENSE.txt","../escape.txt"})
                { using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write("fictional test payload"); }
            }
            var checksum = Path.Combine(dir, "package.sha256");
            var release = Resolve() with { Version = typeof(InstallerChecks).Assembly.GetName().Version! };
            var valid = UpdatePackage.Hash(zip) + "  " + Path.GetFileName(release.Download.LocalPath);
            File.WriteAllText(checksum,valid);
            test("web setup stages only the three expected files and checks EXE version", () =>
            {
                var output = Path.Combine(dir,"valid");
                InstallerPackage.Stage(zip, checksum, release, output);
                Require(Directory.GetFiles(output).Length == 3 && !File.Exists(Path.Combine(dir,"escape.txt")));
                Require(UpdatePackage.Hash(Path.Combine(output,"Piggy - Piggy.exe")) == UpdatePackage.Hash(typeof(InstallerChecks).Assembly.Location));
            });
            test("web setup rejects a valid ZIP whose EXE has the wrong version", () => Reject(() => InstallerPackage.Stage(zip,checksum,release with { Version=new(99,0,0,0) },Path.Combine(dir,"wrong-version"))));
            test("web setup rejects a corrupt download before exposing application payload", () =>
            {
                File.WriteAllText(checksum,new string('0',64) + "  " + Path.GetFileName(release.Download.LocalPath));
                var output = Path.Combine(dir,"corrupt");
                Reject(() => InstallerPackage.Stage(zip,checksum,release,output));
                Require(!File.Exists(Path.Combine(output,"Piggy - Piggy.exe")));
            });
        }
        finally { Directory.Delete(dir,true); }

        if (OperatingSystem.IsWindows())
        {
            test("installed updater synchronizes matching registration but never a portable path", () =>
            {
                var path = @"Software\PiggyInstallerChecks\" + Guid.NewGuid().ToString("N");
                try
                {
                    using var sandbox = Registry.CurrentUser.CreateSubKey(path);
                    var target = Path.Combine(Path.GetTempPath(), "Piggy Registration Fixture", "Piggy - Piggy.exe");
                    Require(!InstallationRegistration.Synchronize(target,new(0,1,0,3),sandbox));
                    using var registration = sandbox.CreateSubKey(InstallationRegistration.KeyPath);
                    registration.SetValue("InstallLocation",Path.GetDirectoryName(target)!);
                    registration.SetValue("DisplayVersion","0.1.0.2");
                    Require(!InstallationRegistration.Synchronize(Path.Combine(Path.GetTempPath(),"portable","Piggy - Piggy.exe"),new(0,1,0,3),sandbox));
                    Require((string)registration.GetValue("DisplayVersion")! == "0.1.0.2");
                    Require(InstallationRegistration.Synchronize(target,new(0,1,0,3),sandbox));
                    Require((string)registration.GetValue("DisplayVersion")! == "0.1.0.3");
                }
                finally { Registry.CurrentUser.DeleteSubKeyTree(path,throwOnMissingSubKey:false); }
            });
        }
    }
    private sealed class Handler(Func<HttpRequestMessage,HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation) => Task.FromResult(respond(request));
    }
}

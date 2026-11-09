using System.Text;
using System.Text.Json;
using MyPiggyBank.Core;

if (args.Length != 2 || args[0] is not ("resolve" or "stage")) return 2;
var directory = Path.GetFullPath(args[1]);
try
{
    if (args[0] == "resolve")
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15), MaxResponseContentBufferSize = 1024 * 1024 };
        var release = await InstallerPackage.ResolveAsync(client);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "release.json"), JsonSerializer.Serialize(release));
        // Windows INI APIs consume Unicode with a BOM.
        File.WriteAllText(Path.Combine(directory, "release.ini"),
            $"[Release]\r\nVersion={AppVersion.Format(release.Version)}\r\nPackage={release.Download.AbsoluteUri}\r\nChecksum={release.Checksum!.AbsoluteUri}\r\n", Encoding.Unicode);
    }
    else
    {
        var release = JsonSerializer.Deserialize<ReleaseInfo>(File.ReadAllText(Path.Combine(directory, "release.json")))
            ?? throw new InvalidDataException("Missing release information.");
        InstallerPackage.Stage(Path.Combine(directory, "package.zip"), Path.Combine(directory, "package.sha256"), release, Path.Combine(directory, "payload"));
    }
    return 0;
}
catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException or ArgumentException)
{
    Directory.CreateDirectory(directory);
    File.WriteAllText(Path.Combine(directory, "error.txt"), "Setup could not prepare the verified release. Check your connection and try again.\r\n\r\n" + e.Message);
    Console.Error.WriteLine(e.Message);
    return 1;
}

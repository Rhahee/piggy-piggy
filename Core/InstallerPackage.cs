using System.Diagnostics;
using System.IO.Compression;

namespace MyPiggyBank.Core;

public static class InstallerPackage
{
    public static async Task<ReleaseInfo> ResolveAsync(HttpClient client, CancellationToken cancellation = default)
    {
        // Resolve the active channel even when repairing an equal application version.
        var check = await new ReleaseChecker(client).CheckChannelAsync(new Version(0, 0, 0, 0), AppVersion.CurrentEpoch, cancellation);
        if (check is not { Status: UpdateStatus.Available, Release.Checksum: not null })
            throw new InvalidDataException(check.Message);
        return check.Release;
    }

    public static void Stage(string zip, string checksum, ReleaseInfo release, string output)
    {
        if (new FileInfo(checksum).Length > 4096 || new FileInfo(zip).Length > UpdatePackage.MaxPackageBytes)
            throw new InvalidDataException("The downloaded package or checksum is too large.");
        var text = File.ReadAllText(checksum);
        if (text.Length > 4096) throw new InvalidDataException("The checksum is too large.");
        var hash = UpdatePackage.ReadChecksum(text, Path.GetFileName(release.Download.LocalPath));
        Directory.CreateDirectory(output);
        var package = UpdatePackage.Extract(zip, hash, output);
        var info = FileVersionInfo.GetVersionInfo(package.Executable);
        var version = new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        if (version != AppVersion.Normalize(release.Version))
            throw new InvalidDataException("The downloaded EXE version does not match the release.");
        using var archive = ZipFile.OpenRead(zip);
        foreach (var name in new[] { "README.md", "Fluent-Icons-LICENSE.txt" })
        {
            var entries = archive.Entries.Where(e => e.FullName == name).ToArray();
            if (entries.Length != 1 || entries[0].Length > 2 * 1024 * 1024)
                throw new InvalidDataException("The package is missing a valid " + name + ".");
            entries[0].ExtractToFile(Path.Combine(output, name), false);
        }
        File.Move(package.Executable, Path.Combine(output, "Piggy - Piggy.exe"));
    }
}

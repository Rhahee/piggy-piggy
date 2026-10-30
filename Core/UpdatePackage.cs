using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace MyPiggyBank.Core;

public sealed record StagedUpdate(string Directory, string Executable, string Sha256);

public static class UpdatePackage
{
    public const long MaxPackageBytes = 250 * 1024 * 1024;
    public const long MaxExecutableBytes = 400 * 1024 * 1024;
    public static string ReadChecksum(string text, string name)
    {
        var match = Regex.Match(text.Trim().TrimStart('\uFEFF'), @"\A([a-fA-F0-9]{64})  " + Regex.Escape(name) + @"\z");
        if (!match.Success) throw new InvalidDataException("The update checksum is invalid.");
        return match.Groups[1].Value;
    }
    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    public static StagedUpdate Extract(string zipPath, string expectedHash, string directory)
    {
        if (!Hash(zipPath).Equals(expectedHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update verification failed. The existing app has not been changed.");
        using var zip = ZipFile.OpenRead(zipPath);
        var entries = zip.Entries.Where(e => e.FullName == "Piggy - Piggy.exe").ToArray();
        if (entries.Length != 1 || entries[0].Length < 2 || entries[0].Length > MaxExecutableBytes)
            throw new InvalidDataException("The update does not contain a valid portable application.");
        // Extract only this exact file; never trust archive paths or execute bundled scripts.
        var exe = Path.Combine(directory,"update.exe");
        entries[0].ExtractToFile(exe,false);
        using (var stream = File.OpenRead(exe))
            if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z') throw new InvalidDataException("The update is not a Windows executable.");
        return new(directory,exe,Hash(exe));
    }
    public static string Replace(string source, string target, string expectedHash, Action launch)
    {
        if (!Hash(source).Equals(expectedHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The staged update has changed.");
        var candidate = target + "." + Guid.NewGuid().ToString("N") + ".new";
        var backup = target + "." + Guid.NewGuid().ToString("N") + ".backup";
        var replaced = false;
        try
        {
            File.Copy(source,candidate,false);
            if (!Hash(candidate).Equals(expectedHash,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Update copy verification failed.");
            File.Replace(candidate,target,backup); replaced = true;
            launch();
            return backup;
        }
        catch
        {
            if (replaced) File.Replace(backup,target,null);
            throw;
        }
        finally { if (File.Exists(candidate)) File.Delete(candidate); }
    }
    public static void Cleanup(string directory,bool keepHelper = false)
    {
        foreach (var name in new[] { "package.zip","update.exe","job.json","ready" })
            try { File.Delete(Path.Combine(directory,name)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        if (!keepHelper)
            try { System.IO.Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}

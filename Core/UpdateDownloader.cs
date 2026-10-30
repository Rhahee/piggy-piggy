namespace MyPiggyBank.Core;

public sealed class UpdateDownloader(HttpClient client)
{
    public async Task<StagedUpdate> DownloadAsync(ReleaseInfo release, IProgress<string> progress, CancellationToken cancellation)
    {
        if (release.Checksum is null) throw new InvalidDataException("This release has no verified update package. Please try a newer release.");
        var directory = Path.Combine(Path.GetTempPath(),"MyPiggyBank-update-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using var checksumResponse = await client.GetAsync(release.Checksum,HttpCompletionOption.ResponseHeadersRead,cancellation);
            checksumResponse.EnsureSuccessStatusCode();
            using var checksumStream = await checksumResponse.Content.ReadAsStreamAsync(cancellation);
            var checksumBytes = new byte[4097]; var checksumLength = 0;
            while (checksumLength < checksumBytes.Length)
            {
                var count = await checksumStream.ReadAsync(checksumBytes.AsMemory(checksumLength),cancellation);
                if (count == 0) break; checksumLength += count;
            }
            if (checksumLength > 4096) throw new InvalidDataException("Update checksum is too large.");
            var hash = UpdatePackage.ReadChecksum(System.Text.Encoding.UTF8.GetString(checksumBytes,0,checksumLength),Path.GetFileName(release.Download.LocalPath));
            using var response = await client.GetAsync(release.Download,HttpCompletionOption.ResponseHeadersRead,cancellation);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;
            if (total > UpdatePackage.MaxPackageBytes) throw new InvalidDataException("The update package is too large.");
            var zip = Path.Combine(directory,"package.zip");
            await using (var input = await response.Content.ReadAsStreamAsync(cancellation))
            await using (var output = File.Create(zip))
            {
                var buffer = new byte[81920]; long received = 0; int count;
                while ((count = await input.ReadAsync(buffer,cancellation)) != 0)
                {
                    received += count;
                    if (received > UpdatePackage.MaxPackageBytes) throw new InvalidDataException("The update package is too large.");
                    await output.WriteAsync(buffer.AsMemory(0,count),cancellation);
                    progress.Report(total > 0 ? $"Downloading update… {received * 100 / total}%" : $"Downloading update… {received / 1024 / 1024} MB");
                }
                if (total.HasValue && received != total.Value) throw new InvalidDataException("The update download is incomplete.");
            }
            cancellation.ThrowIfCancellationRequested(); progress.Report("Verifying update…");
            var package = await Task.Run(() => UpdatePackage.Extract(zip,hash,directory),cancellation);
            return package;
        }
        catch { UpdatePackage.Cleanup(directory); throw; }
    }
}

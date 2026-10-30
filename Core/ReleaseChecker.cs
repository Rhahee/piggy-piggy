using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyPiggyBank.Core;

public enum UpdateStatus { UpToDate, Available, NoRelease, Unavailable }
public sealed record ReleaseInfo(Version Version, Uri Page, Uri Download, string Notes, Uri? Checksum = null, bool IsVersionTransition = false);
public sealed record UpdateCheck(UpdateStatus Status, string Message, ReleaseInfo? Release = null);

public sealed class ReleaseChecker(HttpClient client)
{
    public const string Repository = "Rhahee/piggy-piggy";
    public static readonly Uri Endpoint = new($"https://api.github.com/repos/{Repository}/releases/latest");
    public static readonly Uri ChannelEndpoint = new($"https://raw.githubusercontent.com/{Repository}/main/update-channel.json");

    // Current clients follow an explicit numbering generation instead of historical GitHub Latest.
    // Missing or older channel metadata must never send a reset baseline back to a legacy build.
    public async Task<UpdateCheck> CheckChannelAsync(Version installed, int installedEpoch, CancellationToken cancellation = default)
    {
        try
        {
            using var channelRequest = Request(ChannelEndpoint, installed);
            using var channelResponse = await client.SendAsync(channelRequest, cancellation);
            if (channelResponse.StatusCode == HttpStatusCode.NotFound && installedEpoch == AppVersion.LegacyEpoch)
                return await CheckAsync(installed, cancellation);
            channelResponse.EnsureSuccessStatusCode();
            var channel = UpdateChannel.Parse(await channelResponse.Content.ReadAsStringAsync(cancellation));
            if (!AppVersion.IsKnownEpoch(installedEpoch) || channel.Epoch < installedEpoch)
                return new(UpdateStatus.Unavailable, "The update channel is not ready for this application. Please try again later.");
            var endpoint = new Uri($"https://api.github.com/repos/{Repository}/releases/tags/{channel.Tag}");
            using var releaseRequest = Request(endpoint, installed);
            using var response = await client.SendAsync(releaseRequest, cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(UpdateStatus.Unavailable, "The next release is being prepared. Please check again later.");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellation);
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("tag_name", out var tag) || tag.GetString() != channel.Tag)
                throw new InvalidDataException("The release does not match the update channel.");
            var transition = channel.Epoch > installedEpoch;
            var result = Parse(json, installed, transition, true);
            if (result.Release is not null && AppVersion.Normalize(result.Release.Version) != channel.Version)
                throw new InvalidDataException("The release version does not match the channel.");
            return result.Release is null ? result : result with { Release = result.Release with { IsVersionTransition = transition } };
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return new(UpdateStatus.Unavailable, "The update check timed out. Please try again."); }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidDataException or FormatException or InvalidOperationException)
        { return new(UpdateStatus.Unavailable, "Could not verify updates. Check your connection and try again."); }
    }

    private static HttpRequestMessage Request(Uri endpoint, Version installed)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.UserAgent.ParseAdd($"PiggyPiggy/{AppVersion.Format(installed)}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
        return request;
    }

    public async Task<UpdateCheck> CheckAsync(Version installed, CancellationToken cancellation = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.UserAgent.ParseAdd($"PiggyPiggy/{AppVersion.Format(installed)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await client.SendAsync(request, cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(UpdateStatus.NoRelease,"No published release is available yet. Draft releases are not offered as updates.");
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new(UpdateStatus.Unavailable,"GitHub could not complete the check. Please try again later.");
            response.EnsureSuccessStatusCode();
            return Parse(await response.Content.ReadAsStringAsync(cancellation), installed);
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { return new(UpdateStatus.Unavailable,"The update check timed out. Please try again."); }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidDataException or FormatException or InvalidOperationException)
        { return new(UpdateStatus.Unavailable,"Could not verify updates. Check your connection and try again."); }
    }

    public static UpdateCheck Parse(string json, Version installed) => Parse(json, installed, false, false);
    private static UpdateCheck Parse(string json, Version installed, bool versionTransition, bool channelRelease)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name",out var tagValue) || tagValue.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Missing release version.");
        var tag = tagValue.GetString()!;
        var numericTag = channelRelease && tag.StartsWith("app-v", StringComparison.Ordinal) ? tag[5..] : tag.TrimStart('v');
        if (!Regex.IsMatch(numericTag,@"^\d+\.\d+\.\d+(?:\.\d+)?$") || !Regex.IsMatch(tag,channelRelease?@"^(?:app-v|v)?\d+\.\d+\.\d+(?:\.\d+)?$":@"^v?\d+\.\d+\.\d+(?:\.\d+)?$") || !Version.TryParse(numericTag,out var version) ||
            !root.TryGetProperty("draft",out var draft) || draft.ValueKind != JsonValueKind.False ||
            !root.TryGetProperty("prerelease",out var prerelease) || prerelease.ValueKind != JsonValueKind.False)
            throw new InvalidDataException("Expected a published stable release.");
        if (!versionTransition && AppVersion.Normalize(version) <= AppVersion.Normalize(installed)) return new(UpdateStatus.UpToDate,"You are using the latest available version.");
        var name = $"Piggy-Piggy-{version.ToString()}-win-x64.zip";
        var expected = $"https://github.com/{Repository}/releases/download/{tag}/{name}";
        if (!root.TryGetProperty("assets",out var assets) || assets.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Missing portable package.");
        var package = assets.EnumerateArray().Any(asset => asset.ValueKind == JsonValueKind.Object &&
            asset.TryGetProperty("name",out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == name &&
            asset.TryGetProperty("size",out var size) && size.TryGetInt64(out var length) && length > 0 &&
            asset.TryGetProperty("browser_download_url",out var url) && url.ValueKind == JsonValueKind.String && url.GetString() == expected);
        if (!package) return new(UpdateStatus.Unavailable,"A newer release exists, but its Windows x64 portable package is not ready yet.");
        var checksumUrl = expected + ".sha256";
        var hasChecksum = assets.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.Object &&
            a.TryGetProperty("name",out var n) && n.ValueKind == JsonValueKind.String && n.GetString() == name + ".sha256" &&
            a.TryGetProperty("browser_download_url",out var u) && u.ValueKind == JsonValueKind.String && u.GetString() == checksumUrl &&
            a.TryGetProperty("size",out var z) && z.TryGetInt64(out var bytes) && bytes > 0 && bytes <= 4096);
        var notes = root.TryGetProperty("body",out var body) && body.ValueKind == JsonValueKind.String ? body.GetString() ?? "" : "";
        if (notes.Length > 8000) notes = notes[..8000] + "\n…";
        return new(UpdateStatus.Available,$"Version {AppVersion.Format(version)} is available.",
            new(version,new Uri($"https://github.com/{Repository}/releases/tag/{tag}"),new Uri(expected),notes,hasChecksum ? new Uri(checksumUrl) : null));
    }
}

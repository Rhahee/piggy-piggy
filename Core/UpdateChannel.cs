using System.Text.Json;
using System.Text.RegularExpressions;

namespace MyPiggyBank.Core;

public sealed record UpdateChannel(int Epoch, Version Version, string Tag)
{
    public static UpdateChannel Parse(string json)
    {
        if (json.Length > 4096) throw new InvalidDataException("The update channel is too large.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("schema", out var schema) || schema.ValueKind != JsonValueKind.Number || !schema.TryGetInt32(out var s) || s != 1 ||
            !root.TryGetProperty("epoch", out var epoch) || epoch.ValueKind != JsonValueKind.Number || !epoch.TryGetInt32(out var e) || !AppVersion.IsKnownEpoch(e) ||
            !root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String ||
            !Regex.IsMatch(version.GetString()!, @"^\d+\.\d+\.\d+\.\d+$") || !System.Version.TryParse(version.GetString(), out var v) ||
            !root.TryGetProperty("tag", out var tag) || tag.ValueKind != JsonValueKind.String ||
            tag.GetString() != AppVersion.TagPrefix(e, v) + v)
            throw new InvalidDataException("Invalid update channel.");
        return new(e, v, tag.GetString()!);
    }
}

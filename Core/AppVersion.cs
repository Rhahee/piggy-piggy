namespace MyPiggyBank.Core;

public static class AppVersion
{
    public const int LegacyEpoch = 1, PreviousEpoch = 2, CurrentEpoch = 3;
    public static bool IsKnownEpoch(int value) => value is LegacyEpoch or PreviousEpoch or CurrentEpoch;
    public static string TagPrefix(int epoch, Version version) => epoch >= CurrentEpoch || (epoch == PreviousEpoch && version.Major >= 1) ? "app-v" : "v";
    public static Version Normalize(Version value) => new(value.Major,value.Minor,Math.Max(0,value.Build),Math.Max(0,value.Revision));
    public static string Format(Version value) => Normalize(value).ToString(4);
}

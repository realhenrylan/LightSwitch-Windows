namespace LightSwitch.Core.Updates;

/// <summary>Picks the newest release that is safe to offer: no drafts and no pre-releases.</summary>
public static class ReleaseSelector
{
    public static ReleaseInfo? SelectLatestStable(IEnumerable<ReleaseInfo> releases)
    {
        ReleaseInfo? latest = null;
        foreach (var release in releases)
        {
            if (release.IsDraft || release.IsPrerelease || release.Version.IsPreRelease)
            {
                continue;
            }

            if (latest is null || release.Version > latest.Version)
            {
                latest = release;
            }
        }

        return latest;
    }
}

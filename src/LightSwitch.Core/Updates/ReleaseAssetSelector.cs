namespace LightSwitch.Core.Updates;

/// <summary>
/// Chooses the asset that matches the running installation. The release workflow
/// publishes exactly <c>LightSwitch-&lt;version&gt;-setup.exe</c> and
/// <c>LightSwitch-&lt;version&gt;-portable.zip</c>.
/// </summary>
public static class ReleaseAssetSelector
{
    public static string ExpectedAssetName(SemanticVersion version, InstallationMode mode) =>
        mode == InstallationMode.Installed
            ? $"LightSwitch-{version}-setup.exe"
            : $"LightSwitch-{version}-portable.zip";

    public static ReleaseAsset? Select(ReleaseInfo release, InstallationMode mode)
    {
        var expected = ExpectedAssetName(release.Version, mode);
        var exact = release.FindAsset(expected);
        if (exact is not null)
        {
            return exact;
        }

        // Tolerate extra qualifiers such as an architecture in the asset name, but
        // never accept an asset that belongs to another version or installation mode.
        var suffix = mode == InstallationMode.Installed ? "-setup.exe" : "-portable.zip";
        var versionToken = release.Version.ToString();
        return release.Assets.FirstOrDefault(asset =>
            asset.Name.StartsWith("LightSwitch-", StringComparison.OrdinalIgnoreCase) &&
            asset.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
            asset.Name.Contains(versionToken, StringComparison.OrdinalIgnoreCase));
    }
}

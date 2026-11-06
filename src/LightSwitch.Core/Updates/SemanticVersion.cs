using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace LightSwitch.Core.Updates;

/// <summary>
/// A loose SemVer 2.0.0 version. Build metadata (<c>+abc</c>) is ignored, and
/// one- or two-component tags such as <c>v2.1</c> are accepted for robustness
/// with hand-written GitHub releases.
/// </summary>
public sealed class SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
{
    private readonly string[] _preReleaseIdentifiers;

    private SemanticVersion(int major, int minor, int patch, string? preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        PreRelease = preRelease;
        _preReleaseIdentifiers = preRelease?.Split('.') ?? Array.Empty<string>();
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    /// <summary>Dot-separated pre-release identifiers, or <see langword="null"/> for a stable version.</summary>
    public string? PreRelease { get; }

    public bool IsPreRelease => PreRelease is not null;

    public static bool TryParse([NotNullWhen(true)] string? text, [NotNullWhen(true)] out SemanticVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var span = text.Trim();
        if (span.StartsWith('v') || span.StartsWith('V'))
        {
            span = span[1..];
        }

        if (span.Length == 0)
        {
            return false;
        }

        var plus = span.IndexOf('+');
        if (plus >= 0)
        {
            span = span[..plus];
        }

        string? preRelease = null;
        var dash = span.IndexOf('-');
        if (dash >= 0)
        {
            preRelease = span[(dash + 1)..];
            span = span[..dash];
            if (!IsValidPreRelease(preRelease))
            {
                return false;
            }

            preRelease = NormalizePreRelease(preRelease);
        }

        var parts = span.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out numbers[i]) ||
                numbers[i] < 0)
            {
                return false;
            }
        }

        version = new SemanticVersion(numbers[0], numbers[1], numbers[2], preRelease);
        return true;
    }

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a valid semantic version.");

    public int CompareTo(SemanticVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        // A stable release always sorts after any pre-release of the same version.
        if (PreRelease is null)
        {
            return other.PreRelease is null ? 0 : 1;
        }

        if (other.PreRelease is null)
        {
            return -1;
        }

        var length = Math.Min(_preReleaseIdentifiers.Length, other._preReleaseIdentifiers.Length);
        for (var i = 0; i < length; i++)
        {
            result = CompareIdentifier(_preReleaseIdentifiers[i], other._preReleaseIdentifiers[i]);
            if (result != 0)
            {
                return result;
            }
        }

        return _preReleaseIdentifiers.Length.CompareTo(other._preReleaseIdentifiers.Length);
    }

    public bool Equals(SemanticVersion? other) => other is not null && CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is SemanticVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Patch, PreRelease);

    public override string ToString() =>
        PreRelease is null ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";

    public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

    public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);

    public static bool operator !=(SemanticVersion left, SemanticVersion right) => !left.Equals(right);

    private static bool IsValidPreRelease(string value)
    {
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var identifier in value.Split('.'))
        {
            if (identifier.Length == 0)
            {
                return false;
            }

            foreach (var ch in identifier)
            {
                if (!(char.IsAsciiLetterOrDigit(ch) || ch == '-'))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static string NormalizePreRelease(string value)
    {
        var identifiers = value.Split('.');
        for (var i = 0; i < identifiers.Length; i++)
        {
            if (IsNumericIdentifier(identifiers[i]))
            {
                var trimmed = identifiers[i].TrimStart('0');
                identifiers[i] = trimmed.Length == 0 ? "0" : trimmed;
            }
        }

        return string.Join('.', identifiers);
    }

    private static int CompareIdentifier(string left, string right)
    {
        var leftIsNumeric = IsNumericIdentifier(left);
        var rightIsNumeric = IsNumericIdentifier(right);

        if (leftIsNumeric && rightIsNumeric)
        {
            var leftTrimmed = left.TrimStart('0');
            var rightTrimmed = right.TrimStart('0');
            leftTrimmed = leftTrimmed.Length == 0 ? "0" : leftTrimmed;
            rightTrimmed = rightTrimmed.Length == 0 ? "0" : rightTrimmed;
            if (leftTrimmed.Length != rightTrimmed.Length)
            {
                return leftTrimmed.Length.CompareTo(rightTrimmed.Length);
            }

            return string.CompareOrdinal(leftTrimmed, rightTrimmed);
        }

        if (leftIsNumeric)
        {
            return -1;
        }

        if (rightIsNumeric)
        {
            return 1;
        }

        return string.CompareOrdinal(left, right);
    }

    private static bool IsNumericIdentifier(string value)
    {
        foreach (var ch in value)
        {
            if (ch is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }
}

namespace LightSwitch.Core.Updates;

/// <summary>Pure timing policy for the automatic startup check.</summary>
public static class UpdatePolicy
{
    public static readonly TimeSpan MinimumCheckInterval = TimeSpan.FromHours(24);

    public static bool ShouldAutoCheck(bool enabled, DateTimeOffset? lastCheckUtc, DateTimeOffset now,
        TimeSpan? interval = null)
    {
        if (!enabled)
        {
            return false;
        }

        if (lastCheckUtc is null)
        {
            return true;
        }

        var minimum = interval ?? MinimumCheckInterval;
        if (minimum < TimeSpan.Zero)
        {
            minimum = TimeSpan.Zero;
        }

        // A clock that moved backwards must not suppress checks indefinitely.
        if (lastCheckUtc.Value > now)
        {
            return true;
        }

        return now - lastCheckUtc.Value >= minimum;
    }
}

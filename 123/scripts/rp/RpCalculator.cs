using System;
using CustomRhythia;

namespace CustomRhythia.Rp;

/// <summary>
/// All RP math lives here so it can be tuned in one place.
///
/// Design rules (from spec):
///   1. RP has no cross-map weighting — every map pays out on its own.
///   2. Every speed gets its own difficulty value, computed from the real
///      chart metrics scaled to that speed, not from an arbitrary exponent.
///   3. Accuracy — not raw miss count — is what reduces the payout.
///   4. The account only ever "tops up" toward the highest RP ever earned
///      for a given map (handled in PlayerProfile).
///
/// Math
/// ----
///   density(s)   = notes / second   at speed s = D1 * s
///   cursor(s)    = units / second   at speed s = V1 * s
///   duration(s)  = seconds          at speed s = T1 / s
///
///   raw(s)  = density(s)^2 * cursor(s) * sqrt(duration(s))
///           = raw(1) * s^2.5
///
///   maxRp(s)      = raw(s) * RpScale
///   earnedRp(s,a) = maxRp(s) * (a / 100)^AccExponent
///
/// RpScale is calibrated so a typical 2-minute / 500-note chart at 1x
/// gives ≈200 RP. Both RpScale and AccExponent can be tweaked in the
/// settings screen without recompiling.
/// </summary>
public static class RpCalculator
{
    public const double DefaultRpScale = 0.35;
    public const double DefaultAccExponent = 11.0;

    private const double MinDensity = 0.2;
    private const double MinCursorSpeed = 0.3;

    private static double RpScale => SettingsManager.Current.RpScale;
    private static double AccExponent => SettingsManager.Current.RpAccuracyExponent;

    /// <summary>Maximum theoretical RP for a perfect full-clear at this speed.</summary>
    public static double MaxRpForSpeed(MapStats stats, double speed)
    {
        if (stats == null || speed <= 0) return 0;
        if (stats.DurationSec <= 0 || stats.NoteCount <= 0) return 0;

        double density = stats.AvgDensity * speed;
        double cursor = stats.AvgCursorSpeed * speed;
        double duration = stats.DurationSec / speed;

        if (density < MinDensity) density = MinDensity;
        if (cursor < MinCursorSpeed) cursor = MinCursorSpeed;
        if (duration < 0.001) duration = 0.001;

        double raw = density * density * cursor * Math.Sqrt(duration);
        return raw * RpScale;
    }

    /// <summary>Apply the accuracy penalty to a maximum RP value.</summary>
    public static double ApplyAccuracy(double maxRp, double accuracy)
    {
        if (maxRp <= 0) return 0;
        double acc = Math.Clamp(accuracy, 0.0, 100.0) / 100.0;
        return maxRp * Math.Pow(acc, AccExponent);
    }

    /// <summary>
    /// RP this attempt would earn if it were the best score on the map.
    /// A failed attempt earns nothing.
    /// </summary>
    public static double EarnedRp(MapStats stats, double speed, double accuracy, bool passed)
    {
        if (!passed) return 0;
        return ApplyAccuracy(MaxRpForSpeed(stats, speed), accuracy);
    }
}

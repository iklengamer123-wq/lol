using System;

namespace CustomRhythia.Rp;

/// <summary>
/// All RP math lives here so it can be tuned in one place.
///
/// Design rules:
///   1. RP has no cross-map weighting; every map pays out on its own.
///   2. Every speed gets its own difficulty value, computed from the real
///      chart metrics scaled to that speed.
///   3. Accuracy (not raw miss count) is what reduces the payout.
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
/// RpScale and AccExponent are fixed constants. They used to be
/// user-editable sliders in the settings screen; those sliders were
/// removed because they were confusing without clear context. To retune
/// the whole game, edit the two consts below and rebuild.
///
/// Reference points at RpScale = 0.0085:
///   Dense chart (1896 notes, 2:12):  ~403 RP max
///   Typical 2-min / 500-note at 1x:  ~17 RP max
///   Sparse 1-min / 100-note at 1x:   ~2 RP max
/// </summary>
public static class RpCalculator
{
	/// <summary>
	/// Global multiplier on every payout. Midpoint of the old slider
	/// range (0.005 .. 0.010). Raise it to make every map pay more,
	/// lower it to compress the numbers.
	/// </summary>
	public const double RpScale = 0.0085;

	/// <summary>
	/// Accuracy penalty exponent. Bigger = harsher. At 11.0, one miss on
	/// a chart of about 435 notes (99.77 percent accuracy) costs about
	/// 2.5 percent of the maximum payout.
	///
	/// Examples of the multiplier (accuracy ^ exponent):
	///   100%  -> 100.0%
	///    99%  ->  89.5%
	///    95%  ->  56.9%
	///    90%  ->  31.4%
	/// </summary>
	public const double AccExponent = 11.0;

	private const double MinDensity = 0.2;
	private const double MinCursorSpeed = 0.3;

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

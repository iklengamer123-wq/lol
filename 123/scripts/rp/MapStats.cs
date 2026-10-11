using System;
using CustomRhythia.Map;

namespace CustomRhythia.Rp;

/// <summary>
/// Speed-independent facts about a chart. Everything the RP formula needs
/// that does NOT change when you play the map at a different speed:
///   • how many notes there are
///   • how long the map lasts at 1x
///   • how far the cursor has to travel across the whole chart
///
/// Speed-specific values (notes/second, cursor units/second, duration)
/// are derived in RpCalculator.
/// </summary>
public class MapStats
{
    public int NoteCount { get; set; }
    public double DurationMs { get; set; }
    public double TotalCursorDistance { get; set; }

    public double DurationSec => DurationMs / 1000.0;

    /// <summary>Average notes per second across the whole map (at 1x).</summary>
    public double AvgDensity => DurationSec > 0 ? NoteCount / DurationSec : 0;

    /// <summary>Average cursor travel per second across the whole map (at 1x).</summary>
    public double AvgCursorSpeed => DurationSec > 0 ? TotalCursorDistance / DurationSec : 0;

    public static MapStats Compute(Note[] notes, int lengthMs)
    {
        var s = new MapStats
        {
            NoteCount = notes?.Length ?? 0,
            DurationMs = Math.Max(1, lengthMs),
        };

        if (notes == null || notes.Length < 2) return s;

        double total = 0;
        for (int i = 1; i < notes.Length; i++)
        {
            double dx = notes[i].X - notes[i - 1].X;
            double dy = notes[i].Y - notes[i - 1].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }
        s.TotalCursorDistance = total;
        return s;
    }
}

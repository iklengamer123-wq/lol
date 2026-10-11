using System.Collections.Generic;

namespace CustomRhythia.Rp;

/// <summary>One historical attempt, stored on the profile.</summary>
public class RpScoreEntry
{
    public string MapName { get; set; } = "";   // folder name (unique key)
    public string MapTitle { get; set; } = "";  // pretty title for display
    public double RP { get; set; }              // potential RP of this attempt
    public double RPAwarded { get; set; }       // delta actually added to the account
    public uint Misses { get; set; }
    public double Accuracy { get; set; }
    public string Grade { get; set; } = "F";
    public double Speed { get; set; } = 1.0;
    public double StartFrom { get; set; }
    public bool Passed { get; set; }
    public double Timestamp { get; set; }       // unix seconds
}

/// <summary>Per-map account record: how much RP this map has ever produced.</summary>
public class MapRpRecord
{
    public double MaxRPEarned { get; set; }
    public double BestSpeed { get; set; } = 1.0;
}

/// <summary>Serialized player profile.</summary>
public class PlayerProfileData
{
    public string Nickname { get; set; } = "";
    public double TotalRP { get; set; }
    public Dictionary<string, MapRpRecord> Maps { get; set; } = new();
    public List<RpScoreEntry> Scores { get; set; } = new();
}

/// <summary>What happened when an attempt was submitted to the profile.</summary>
public class RpAwardResult
{
    public double EarnedRP;       // RP the attempt would be worth
    public double DeltaAwarded;   // how much was actually added to the account
    public double PreviousMax;    // map's previous personal best
    public double NewMax;         // map's new personal best
    public bool IsNewRecord;
    public bool Skipped;          // e.g. StartFrom > 0, or no stats available
}

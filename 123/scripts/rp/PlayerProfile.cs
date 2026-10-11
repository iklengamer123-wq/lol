using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CustomRhythia.Game;
using CustomRhythia.Map;
using Godot;

namespace CustomRhythia.Rp;

/// <summary>
/// The persistent player account. Owns the nickname, the running total RP,
/// and the per-map "highest RP ever earned" records that drive the top-up
/// rule.
///
/// Top-up rule (from spec):
///   • Each map has ONE number that matters: the highest RP ever earned on
///     it, across all speeds and all attempts.
///   • A new attempt only pays out if it would raise that number. The
///     amount added to the account is exactly the difference.
///   • So a perfect 1.45x clear pays more than a perfect 1x clear; but
///     once you hit the 1.45x ceiling, further clears of the same map
///     give nothing — on ANY speed.
/// </summary>
public static class PlayerProfile
{
    private const string FileName = "profile.json";
    private const int MaxScoresStored = 1000;

    public static PlayerProfileData Data { get; private set; } = new();
    public static bool IsFirstLaunch { get; private set; }

    public static event Action Changed;

    private static string Path => $"{Constants.UserFolder}/{FileName}";

    public static void Load()
    {
        Directory.CreateDirectory(Constants.UserFolder);

        if (!File.Exists(Path))
        {
            Data = new PlayerProfileData { Nickname = "" };
            IsFirstLaunch = true;
            return;
        }

        try
        {
            var json = File.ReadAllText(Path);
            var loaded = JsonSerializer.Deserialize<PlayerProfileData>(json, JsonOpts);
            Data = loaded ?? new PlayerProfileData();
            Data.Maps ??= new Dictionary<string, MapRpRecord>();
            Data.Scores ??= new List<RpScoreEntry>();
            IsFirstLaunch = string.IsNullOrEmpty(Data.Nickname);
        }
        catch (Exception ex)
        {
            GD.PushError("Profile load failed: " + ex.Message);
            Data = new PlayerProfileData();
            IsFirstLaunch = true;
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(Constants.UserFolder);
            File.WriteAllText(Path, JsonSerializer.Serialize(Data, JsonOpts));
        }
        catch (Exception ex)
        {
            GD.PushError("Profile save failed: " + ex.Message);
        }
    }

    public static void SetNickname(string nick)
    {
        if (string.IsNullOrWhiteSpace(nick)) return;
        Data.Nickname = nick.Trim();
        if (Data.Nickname.Length > 32) Data.Nickname = Data.Nickname[..32];
        IsFirstLaunch = false;
        Save();
        Changed?.Invoke();
    }

    /// <summary>
    /// Submit a finished attempt. Returns what was awarded and the state of
    /// the map's personal record before and after.
    /// </summary>
    public static RpAwardResult AwardAttempt(Attempt attempt)
    {
        var result = new RpAwardResult();

        if (attempt?.Map == null) { result.Skipped = true; return result; }

        // Only full runs (no StartFrom) are eligible for RP. Skipped runs
        // don't count toward the account at all.
        if (attempt.StartFrom > 1.0) { result.Skipped = true; return result; }

        var stats = attempt.Map.Stats;
        if (stats == null || stats.NoteCount <= 0) { result.Skipped = true; return result; }

        bool passed = attempt.FullClear;
        double earned = RpCalculator.EarnedRp(stats, attempt.Speed, attempt.Accuracy, passed);
        result.EarnedRP = earned;

        string key = attempt.Map.Name;
        if (!Data.Maps.TryGetValue(key, out var rec))
        {
            rec = new MapRpRecord();
            Data.Maps[key] = rec;
        }

        result.PreviousMax = rec.MaxRPEarned;

        if (earned > rec.MaxRPEarned + 0.0001)
        {
            double delta = earned - rec.MaxRPEarned;
            result.DeltaAwarded = delta;
            result.IsNewRecord = true;

            Data.TotalRP += delta;
            rec.MaxRPEarned = earned;
            rec.BestSpeed = attempt.Speed;
        }

        result.NewMax = rec.MaxRPEarned;

        // Always record the score in history for the profile screen, even
        // if it awarded nothing.
        var entry = new RpScoreEntry
        {
            MapName = key,
            MapTitle = attempt.Map.PrettyTitle,
            RP = earned,
            RPAwarded = result.DeltaAwarded,
            Misses = attempt.Misses,
            Accuracy = attempt.Accuracy,
            Grade = Leaderboard.GradeFromAccuracy(attempt.Accuracy, passed),
            Speed = attempt.Speed,
            StartFrom = attempt.StartFrom,
            Passed = passed,
            Timestamp = Time.GetUnixTimeFromSystem(),
        };

        Data.Scores.Add(entry);
        if (Data.Scores.Count > MaxScoresStored)
        {
            Data.Scores.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            Data.Scores.RemoveRange(0, Data.Scores.Count - MaxScoresStored);
        }

        Save();
        Changed?.Invoke();
        return result;
    }

    /// <summary>Highest RP ever earned for this map (across all speeds).</summary>
    public static double GetMapMaxRP(string mapName)
    {
        if (string.IsNullOrEmpty(mapName)) return 0;
        return Data.Maps.TryGetValue(mapName, out var rec) ? rec.MaxRPEarned : 0;
    }

    public static List<RpScoreEntry> TopScores(int count)
    {
        return Data.Scores
            .Where(s => s.Passed)
            .OrderByDescending(s => s.RP)
            .ThenByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    public static List<RpScoreEntry> RecentScores(int count)
    {
        return Data.Scores
            .OrderByDescending(s => s.Timestamp)
            .Take(count)
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
    };
}

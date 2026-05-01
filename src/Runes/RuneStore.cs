using System.Text.Json;
using Godot;
using healerfantasy.Runes;

namespace healerfantasy;

/// <summary>
/// Persistent store for the player's acquired runes across all runs.
///
/// Saved to <c>user://runes.save</c> and survives game restarts.
///
/// Acquiring rune N requires completing a full run with all runes 1..N-1
/// active.  The Queen of the Frozen Wastes will only drop rune N when the
/// current run had exactly runes 1..N-1 enabled (handled in
/// <see cref="VictoryScreen"/>).
/// </summary>
public static class RuneStore
{
    // ── constants ─────────────────────────────────────────────────────────────

    const string FileSavePath = "user://runes.save";

    /// <summary>Total number of runes in the game.</summary>
    public const int TotalRunes = 4;

    // ── data ──────────────────────────────────────────────────────────────────

    public sealed class RuneData
    {
        /// <summary>
        /// How many runes have been permanently unlocked (0–4).
        /// Rune N is unlocked when <c>AcquiredRuneCount >= N</c>.
        /// </summary>
        public int AcquiredRuneCount { get; set; } = 0;
    }

    // ── in-memory state ───────────────────────────────────────────────────────

    static RuneData _data = LoadFromDisk();

    /// <summary>How many runes have been permanently unlocked (0–4).</summary>
    public static int AcquiredRuneCount => _data.AcquiredRuneCount;

    /// <summary>Returns <c>true</c> if rune <paramref name="index"/> has been acquired.</summary>
    public static bool HasRune(RuneIndex index) => (int)index <= _data.AcquiredRuneCount;

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Unlocks the next rune (increments <see cref="AcquiredRuneCount"/> by 1)
    /// and saves to disk.  No-op if all runes are already acquired.
    /// </summary>
    public static void UnlockNextRune()
    {
        if (_data.AcquiredRuneCount >= TotalRunes) return;
        _data.AcquiredRuneCount++;
        SaveToDisk();
    }

    // ── persistence ───────────────────────────────────────────────────────────

    static RuneData LoadFromDisk()
    {
        if (!FileAccess.FileExists(FileSavePath))
            return new RuneData();
        try
        {
            using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Read);
            return JsonSerializer.Deserialize<RuneData>(file.GetAsText()) ?? new RuneData();
        }
        catch
        {
            return new RuneData();
        }
    }

    static void SaveToDisk()
    {
        using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Write);
        file.StoreLine(JsonSerializer.Serialize(_data));
    }

    /// <summary>
    /// Deletes the rune save file and resets in-memory state to zero acquired runes.
    /// </summary>
    public static void DeleteSaveFile()
    {
        if (FileAccess.FileExists(FileSavePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FileSavePath));
        _data = new RuneData();
    }
}

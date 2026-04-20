using System.Collections.Generic;

namespace healerfantasy.SpellSystem;

/// <summary>Immutable record of a single completed spell cast.</summary>
public readonly struct SpellCastRecord
{
    /// <summary>The spell's name (used as its stable identifier).</summary>
    public string SpellId  { get; init; }

    /// <summary>Game time in seconds when the cast landed.</summary>
    public double Timestamp { get; init; }
}

/// <summary>
/// Tracks every spell cast made by a character.
/// Attached to <see cref="Character"/> and written by <see cref="SpellPipeline"/>.
/// </summary>
public class SpellHistory
{
    readonly List<SpellCastRecord> _records = new();

    // ── write ────────────────────────────────────────────────────────────────
    public void Record(string spellId, double timestamp)
        => _records.Add(new SpellCastRecord { SpellId = spellId, Timestamp = timestamp });

    // ── queries ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Number of distinct spell IDs cast within the last <paramref name="seconds"/> seconds.
    /// </summary>
    public int UniqueSpellsInLastSeconds(double currentTime, float seconds)
    {
        double cutoff = currentTime - seconds;
        var seen = new HashSet<string>();
        foreach (var r in _records)
            if (r.Timestamp >= cutoff)
                seen.Add(r.SpellId);
        return seen.Count;
    }

    /// <summary>
    /// Total casts of <paramref name="spellId"/> within the last <paramref name="seconds"/> seconds.
    /// </summary>
    public int CastsOfSpellInLastSeconds(string spellId, double currentTime, float seconds)
    {
        double cutoff = currentTime - seconds;
        int count = 0;
        foreach (var r in _records)
            if (r.SpellId == spellId && r.Timestamp >= cutoff)
                count++;
        return count;
    }

    /// <summary>All recorded casts, in chronological order.</summary>
    public IReadOnlyList<SpellCastRecord> All => _records;
}

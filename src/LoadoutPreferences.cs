using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using healerfantasy.Runes;
using healerfantasy.SpellResources;

namespace healerfantasy;

/// <summary>
/// Persists the player's preferred spell loadout and rune selections across
/// game restarts.
///
/// Only spell <em>names</em> (plain strings) are written to disk. On load the
/// names are resolved back to live objects via <see cref="SpellRegistry.AllSpells"/>.
///
/// Saved to <c>user://loadout-preferences.save</c>.
///
/// Note: Talents are no longer persisted here — they are earned during each run
/// via the victory screen and reset at the start of every new run.
/// </summary>
public static class LoadoutPreferences
{
    const string FileSavePath = "user://loadout-preferences.save";

    // ── data model ────────────────────────────────────────────────────────────

    public sealed class PreferencesData
    {
        /// <summary>
        /// Spell names for each loadout slot, preserving slot positions.
        /// An empty string means the slot is empty.
        /// Length matches <see cref="Player.MaxSpellSlots"/>.
        /// </summary>
        public List<string> SelectedSpellNames { get; set; } = new();

        /// <summary>
        /// 1-based <see cref="RuneIndex"/> values for runes that were active
        /// when last saved.  Stored as ints for JSON friendliness.
        /// Null-safe on old saves (deserialises to an empty list).
        /// </summary>
        public List<int> ActiveRuneIndices { get; set; } = new();

        /// <summary>
        /// The player's preferred spell school affinity, stored as the enum
        /// name string (e.g. "Holy", "Nature").  Null means no preference set.
        /// Null-safe on old saves that predate affinity persistence.
        /// </summary>
        public string? SchoolAffinity { get; set; } = null;
    }

    // ── in-memory state ───────────────────────────────────────────────────────

    static PreferencesData _data = LoadFromDisk();

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>True if the player has saved at least one spell.</summary>
    public static bool HasSavedSpells =>
        _data.SelectedSpellNames.Any(n => !string.IsNullOrEmpty(n));

    /// <summary>
    /// Returns an array of saved spell resources, preserving slot positions.
    /// Unrecognised or empty name entries become null (empty slot).
    /// </summary>
    public static SpellResource?[] SavedSpells
    {
        get
        {
            var result = new SpellResource?[Player.MaxSpellSlots];
            var names = _data.SelectedSpellNames;
            for (var i = 0; i < names.Count && i < result.Length; i++)
            {
                var name = names[i];
                if (!string.IsNullOrEmpty(name))
                    result[i] = SpellRegistry.AllSpells.FirstOrDefault(s => s.Name == name);
            }
            return result;
        }
    }

    /// <summary>
    /// 1-based <see cref="RuneIndex"/> values that were active at last save.
    /// Returns an empty list on old saves that predate rune persistence.
    /// </summary>
    public static IReadOnlyList<int> SavedActiveRuneIndices =>
        _data.ActiveRuneIndices.AsReadOnly();

    /// <summary>
    /// The school affinity the player last selected, or null if none was set.
    /// Returns null on old saves that predate affinity persistence.
    /// </summary>
    public static SpellSchool? SavedSchoolAffinity
    {
        get
        {
            if (string.IsNullOrEmpty(_data.SchoolAffinity)) return null;
            return Enum.TryParse<SpellSchool>(_data.SchoolAffinity, out var school) ? school : null;
        }
    }

    /// <summary>
    /// Persists the player's school affinity preference.
    /// Call this whenever the player changes or clears their affinity.
    /// </summary>
    public static void SaveSchoolAffinity(SpellSchool? school)
    {
        _data.SchoolAffinity = school?.ToString();
        SaveToDisk();
    }

    /// <summary>
    /// Persists the player's current rune selection.  Call this whenever
    /// the player activates or deactivates a rune at the Rune Table.
    /// </summary>
    public static void SaveActiveRunes(IEnumerable<RuneIndex> runes)
    {
        _data.ActiveRuneIndices = runes.Select(r => (int)r).ToList();
        SaveToDisk();
    }

    /// <summary>
    /// Persists the current spell loadout array, preserving slot positions.
    /// Call this whenever the player equips or unequips a spell in the Overworld.
    /// </summary>
    public static void SaveSpells(SpellResource?[] spells)
    {
        // Store one entry per slot; empty slots become empty strings.
        _data.SelectedSpellNames = spells
            .Select(s => s?.Name ?? string.Empty)
            .ToList();
        SaveToDisk();
    }

    // ── persistence ───────────────────────────────────────────────────────────

    static PreferencesData LoadFromDisk()
    {
        if (!FileAccess.FileExists(FileSavePath))
            return new PreferencesData();
        try
        {
            using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Read);
            return JsonSerializer.Deserialize<PreferencesData>(file.GetAsText()) ?? new PreferencesData();
        }
        catch
        {
            return new PreferencesData();
        }
    }

    static void SaveToDisk()
    {
        using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Write);
        file.StoreLine(JsonSerializer.Serialize(_data));
    }

    /// <summary>
    /// Deletes the loadout preferences save file from disk and resets all
    /// in-memory state to defaults.
    /// </summary>
    public static void DeleteSaveFile()
    {
        if (FileAccess.FileExists(FileSavePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FileSavePath));
        _data = new PreferencesData();
    }
}

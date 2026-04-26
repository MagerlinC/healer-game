using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.Talents;

namespace healerfantasy;

/// <summary>
/// Persists the player's preferred spell loadout and talent selections across
/// game restarts.
///
/// Only spell/talent <em>names</em> (plain strings) are written to disk, so
/// there is no attempt to serialize <see cref="TalentDefinition.Configure"/>
/// delegates, Godot <see cref="Resource"/> objects, or textures. On load the
/// names are resolved back to live objects via
/// <see cref="TalentRegistry.AllTalents"/> and
/// <see cref="SpellRegistry.AllSpells"/>.
///
/// Saved to <c>user://loadout-preferences.save</c>.
/// This is intentionally separate from <see cref="PlayerProgressStore"/>, which
/// handles character progression (level / XP / talent points).
/// </summary>
public static class LoadoutPreferences
{
    const string FileSavePath = "user://loadout-preferences.save";

    // ── data model ────────────────────────────────────────────────────────────

    public sealed class PreferencesData
    {
        /// <summary>Names of selected talents, in the order they were chosen.</summary>
        public List<string> SelectedTalentNames { get; set; } = new();

        /// <summary>
        /// Spell names for each loadout slot, preserving slot positions.
        /// An empty string means the slot is empty.
        /// Length matches <see cref="Player.MaxSpellSlots"/>.
        /// </summary>
        public List<string> SelectedSpellNames { get; set; } = new();
    }

    // ── in-memory state ───────────────────────────────────────────────────────

    static PreferencesData _data = LoadFromDisk();

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>True if the player has saved at least one spell.</summary>
    public static bool HasSavedSpells =>
        _data.SelectedSpellNames.Any(n => !string.IsNullOrEmpty(n));

    /// <summary>True if the player has saved at least one talent.</summary>
    public static bool HasSavedTalents =>
        _data.SelectedTalentNames.Count > 0;

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
    /// Returns the saved talent definitions, looked up by name from
    /// <see cref="TalentRegistry.AllTalents"/>. Unrecognised names are skipped.
    /// </summary>
    public static IReadOnlyList<TalentDefinition> SavedTalents =>
        TalentRegistry.AllTalents
            .Where(t => _data.SelectedTalentNames.Contains(t.Name))
            .ToList();

    /// <summary>
    /// Persists the given talent selection. Call this whenever the player
    /// confirms a talent change in the Overworld.
    /// </summary>
    public static void SaveTalents(IEnumerable<TalentDefinition> talents)
    {
        _data.SelectedTalentNames = talents.Select(t => t.Name).ToList();
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
    /// in-memory state (spell selections and talent choices) to defaults.
    /// </summary>
    public static void DeleteSaveFile()
    {
        if (FileAccess.FileExists(FileSavePath))
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FileSavePath));
        _data = new PreferencesData();
    }
}

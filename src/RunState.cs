#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.Talents;

namespace healerfantasy;

/// <summary>
/// Autoload singleton that persists run-level state across scenes.
///
/// The Overworld writes to this before calling ChangeSceneToFile("World.tscn").
/// Player._Ready() reads from it to populate EquippedSpells and Talents.
///
/// Call <see cref="Reset"/> when starting a completely fresh run from the Main Menu
/// so previous run choices don't bleed into the next run.
/// </summary>
public partial class RunState : Node
{
    public static RunState Instance { get; private set; } = null!;

    /// <summary>
    /// The 6 spells the player chose in the Overworld.
    /// Null slots are empty. Initialised to defaults so a direct BossFight
    /// launch (e.g. in the editor) still works without visiting the Overworld.
    /// </summary>
    public SpellResource?[] SelectedSpells { get; } = new SpellResource?[Player.MaxSpellSlots];

    /// <summary>
    /// Talent definitions selected in the Overworld.
    /// BossFight instantiates these into <see cref="Character.Talents"/> via
    /// <c>def.CreateTalent()</c> in Player._Ready().
    /// </summary>
    public List<TalentDefinition> SelectedTalentDefs { get; } = new();

    public bool HasLoadout => SelectedSpells.Any(s => s != null);

    // ── lifecycle ─────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        InitDefaultSpells();
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void SetSpells(SpellResource?[] spells)
    {
        for (var i = 0; i < Player.MaxSpellSlots; i++)
            SelectedSpells[i] = i < spells.Length ? spells[i] : null;
    }

    public void SetTalents(IEnumerable<TalentDefinition> defs)
    {
        SelectedTalentDefs.Clear();
        SelectedTalentDefs.AddRange(defs);
    }

    /// <summary>
    /// Wipes talent selections and resets spells to defaults.
    /// Call this when the player returns to the Main Menu to start fresh.
    /// </summary>
    public void Reset()
    {
        SelectedTalentDefs.Clear();
        InitDefaultSpells();
    }

    // ── private ───────────────────────────────────────────────────────────────

    void InitDefaultSpells()
    {
        var defaults = new[]
        {
            "Touch of Light", "Wave of Incandescence", "Renewing Bloom",
            "Reinvigorate", "Burst of Light", "Decay"
        };
        for (var i = 0; i < Player.MaxSpellSlots; i++)
            SelectedSpells[i] = i < defaults.Length
                ? SpellRegistry.AllSpells.FirstOrDefault(s => s.Name == defaults[i])
                : null;
    }
}

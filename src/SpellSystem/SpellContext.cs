using System.Collections.Generic;
using healerfantasy.SpellResources;

namespace healerfantasy.SpellSystem;

/// <summary>
/// Carries all data about a single spell cast through the modifier pipeline.
/// Built by <see cref="SpellPipeline.Cast"/> and passed to every
/// <see cref="ISpellModifier"/> in priority order.
/// </summary>
public class SpellContext
{
    /// <summary>The character who is casting the spell.</summary>
    public Character Caster { get; set; }

    /// <summary>
    /// All characters the spell will affect.
    /// For single-target spells this is a one-element list; for group spells it
    /// is populated by <see cref="SpellResource.ResolveTargets"/>.
    /// </summary>
    public List<Character> Targets { get; set; } = new();

    /// <summary>The spell being cast.</summary>
    public SpellResource Spell { get; set; }

    /// <summary>The raw, unmodified value taken from the spell definition.</summary>
    public float BaseValue { get; set; }

    /// <summary>
    /// The value modifiers should read and write during <see cref="ISpellModifier.OnCalculate"/>.
    /// Starts equal to <see cref="BaseValue"/> after stat multipliers are applied.
    /// This is the number ultimately passed to the spell's Apply method.
    /// </summary>
    public float FinalValue { get; set; }

    /// <summary>Tags describing this cast. Modifiers may read and add tags.</summary>
    public SpellTags Tags { get; set; }

    /// <summary>Game time in seconds when the cast completed (Time.GetTicksMsec / 1000).</summary>
    public double Timestamp { get; set; }

    /// <summary>Character stats computed at the start of this cast.</summary>
    public CharacterStats CasterStats { get; set; }

    /// <summary>
    /// Bonus duration in seconds to add to any effect applied by this spell.
    /// Set by <see cref="ISpellModifier"/>s (e.g. BandOfTheVoid) during
    /// <see cref="ISpellModifier.OnCalculate"/>. DoT/HoT spell Apply methods
    /// should read this and add it to their base duration.
    /// </summary>
    public float EffectDurationBonus { get; set; } = 0f;

    /// <summary>Convenience accessor — the primary (first) target, or null if the list is empty.</summary>
    public Character Target => Targets.Count > 0 ? Targets[0] : null;

    /// <summary>
    /// Set to <c>false</c> by a spell's <see cref="SpellResource.Apply"/> implementation
    /// when it considers itself to have had no actual effect on the world.
    /// Read by <see cref="Player"/> to decide whether to start the cooldown —
    /// used by Dispel so that casting it at a target with nothing to cleanse
    /// does not consume the cooldown.
    /// Defaults to <c>true</c> so all spells that don't opt in are unaffected.
    /// </summary>
    public bool WasEffective { get; set; } = true;
}

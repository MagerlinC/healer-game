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

    /// <summary>Convenience accessor — the primary (first) target, or null if the list is empty.</summary>
    public Character Target => Targets.Count > 0 ? Targets[0] : null;
}

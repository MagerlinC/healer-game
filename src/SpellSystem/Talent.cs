using System.Collections.Generic;

namespace healerfantasy.SpellSystem;

/// <summary>
/// A learnable talent that contributes modifiers to the spell pipeline
/// and/or to the character's stat computation.
///
/// A single talent may carry both kinds of modifier — for example a talent
/// that increases CritChance (character modifier) AND reacts to crits
/// by applying a buff (spell modifier).
/// </summary>
public class Talent
{
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Modifiers injected into every spell cast by the owning character.</summary>
    public List<ISpellModifier> SpellModifiers { get; } = new();

    /// <summary>Modifiers applied when computing the character's final stats.</summary>
    public List<ICharacterModifier> CharacterModifiers { get; } = new();
}

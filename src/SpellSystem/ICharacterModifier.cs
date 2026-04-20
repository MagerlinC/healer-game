namespace healerfantasy.SpellSystem;

/// <summary>
/// Modifies a character's base <see cref="CharacterStats"/>.
/// Implement this on a <see cref="Talent"/> to affect stats like CritChance
/// or DamageMultiplier rather than individual spell casts.
/// </summary>
public interface ICharacterModifier
{
    /// <summary>
    /// Called once per stat-computation pass.
    /// Mutate <paramref name="stats"/> in place — add, subtract, or multiply values.
    /// </summary>
    void Modify(CharacterStats stats);
}

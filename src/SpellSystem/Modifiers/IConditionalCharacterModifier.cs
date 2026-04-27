namespace healerfantasy.SpellSystem;

/// <summary>
/// A character modifier that receives the character being modified.
/// Use when the stat change is conditional on the character's current
/// runtime state — for example, active effects, current health, or
/// equipped items on other characters.
///
/// Implement this on a <see cref="CharacterEffect"/> (or a
/// <see cref="Talent"/>) alongside, or instead of,
/// <see cref="ICharacterModifier"/> when the plain
/// <c>Modify(CharacterStats)</c> overload lacks the context you need.
///
/// Picked up automatically by <see cref="Character.GetCharacterStats"/>
/// for active effects, just as <see cref="ICharacterModifier"/> is.
/// </summary>
public interface IConditionalCharacterModifier
{
    /// <summary>
    /// Called once per stat-computation pass.
    /// Mutate <paramref name="stats"/> in place based on the current
    /// state of <paramref name="character"/>.
    /// </summary>
    void Modify(CharacterStats stats, Character character);
}

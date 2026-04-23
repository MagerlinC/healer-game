using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// Hardens the target's skin with nature magic, reducing all incoming damage
/// by <see cref="DamageReductionFraction"/> for the effect's duration.
///
/// Implements <see cref="ICharacterModifier"/> so <see cref="Character.GetCharacterStats"/>
/// automatically incorporates the mitigation while the buff is active.
/// </summary>
public partial class BarkskinEffect : CharacterEffect, ICharacterModifier
{
    /// <summary>Fraction of incoming damage that is negated. 0.25 = 25% reduction.</summary>
    public float DamageReductionFraction { get; }

    public BarkskinEffect(float duration, float damageReductionFraction = 0.25f)
        : base(duration, 0f)
    {
        EffectId = "Barkskin";
        DamageReductionFraction = damageReductionFraction;
    }

    // ── ICharacterModifier ────────────────────────────────────────────────────

    /// <summary>
    /// Reduces the DamageTakenMultiplier so that all incoming damage is
    /// reduced by <see cref="DamageReductionFraction"/> while active.
    /// </summary>
    public void Modify(CharacterStats stats)
    {
        stats.DamageTakenMultiplier *= (1f - DamageReductionFraction);
    }
}

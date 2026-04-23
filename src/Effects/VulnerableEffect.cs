using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// A void-infused debuff that tears through the target's defences, causing
/// them to take <see cref="DamageAmplification"/> more damage from all sources
/// while the effect is active.
///
/// Applied by Soul Shatter. Dispellable.
/// Implements <see cref="ICharacterModifier"/> so the amplification is
/// automatically included in every <see cref="Character.TakeDamage"/> call.
/// </summary>
public partial class VulnerableEffect : CharacterEffect, ICharacterModifier
{
    /// <summary>
    /// Multiplicative increase to all damage received.
    /// 0.15 means the target takes 15% more damage from every hit.
    /// </summary>
    public float DamageAmplification { get; }

    public VulnerableEffect(float duration, float damageAmplification = 0.15f)
        : base(duration, 0f)
    {
        EffectId = "Vulnerable";
        DamageAmplification = damageAmplification;
        IsHarmful = true;
        IsDispellable = true;
    }

    // ── ICharacterModifier ────────────────────────────────────────────────────

    public void Modify(CharacterStats stats)
    {
        stats.DamageTakenMultiplier *= (1f + DamageAmplification);
    }
}

using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// A holy buff that suffuses the target with divine energy, increasing all
/// damage they deal by <see cref="DamageBonus"/> for the effect's duration.
///
/// Applied by the Radiant Infusion talent after a healing spell lands.
/// Implements <see cref="ICharacterModifier"/> so the bonus is automatically
/// applied through <see cref="Character.GetCharacterStats"/>.
/// </summary>
public partial class RadiantInfusionEffect : CharacterEffect, ICharacterModifier
{
	/// <summary>Flat addition to DamageMultiplier while active. 0.15 = +15% damage.</summary>
	public float DamageBonus { get; }

	public RadiantInfusionEffect(float duration, float damageBonus = 0.15f)
		: base(duration, 0f)
	{
		EffectId = "RadiantInfusion";
		DamageBonus = damageBonus;
	}

	// ── ICharacterModifier ────────────────────────────────────────────────────

	public void Modify(CharacterStats stats)
	{
		stats.IncreasedDamage += DamageBonus;
	}
}
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// Bringer of Death's Death Mark debuff.
/// Brands the target with a necrotic curse, dealing damage per second for
/// 12 seconds, and increases all damage the target takes by 25%.
/// Can be cleansed by Dispel.
/// </summary>
public partial class DeathMarkEffect : DamageOverTimeEffect, ICharacterModifier
{
	/// <summary>Fraction of extra damage the marked target takes from all sources.</summary>
	public const float DamageTakenIncrease = 0.25f;

	public DeathMarkEffect(float damagePerTick) : base(damagePerTick, 12f, 1f)
	{
		EffectId = "DeathMark";
		School = SpellSchool.Void;
	}

	// ── ICharacterModifier ─────────────────────────────────────────────────────

	/// <summary>
	/// While Death Mark is active, the affected character takes 25% more damage
	/// from all sources. Picked up automatically by <see cref="Character.GetCharacterStats"/>
	/// and applied in <see cref="Character.TakeDamage"/>.
	/// </summary>
	public void Modify(CharacterStats stats)
	{
		stats.DamageTakenMultiplier += DamageTakenIncrease;
	}
}
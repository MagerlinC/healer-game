using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// The Countess's Crimson Curse debuff.
///
/// Deals <see cref="DamagePerTick"/> damage per second for 10 seconds AND
/// reduces all healing received by the target by 50%.
///
/// Dispel interaction
/// ──────────────────
/// The debuff is dispellable. Dispelling it early removes both the DoT and
/// the healing reduction — no secondary effect on the boss.
/// </summary>
public partial class CrimsonCurseEffect : DamageOverTimeEffect, ICharacterModifier
{
	/// <summary>Fraction by which incoming healing is reduced while this debuff is active.</summary>
	public const float HealingReduction = 0.5f;

	public CrimsonCurseEffect(float damagePerTick) : base(damagePerTick, 10f, 1f)
	{
		EffectId = "CrimsonCurse";
		School = SpellSchool.Void;
		IsDispellable = true;
		IsHarmful = true;
	}

	// ── ICharacterModifier ────────────────────────────────────────────────────

	/// <summary>
	/// While Crimson Curse is active the target receives 50% less healing from
	/// all sources. Picked up automatically by <see cref="Character.GetCharacterStats"/>
	/// and applied in <see cref="Character.Heal"/>.
	/// </summary>
	public void Modify(CharacterStats stats)
	{
		stats.HealingReceivedMultiplier -= HealingReduction;
	}
}

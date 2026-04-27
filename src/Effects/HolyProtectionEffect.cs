using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// Passive tracker applied to friendly characters by
/// <see cref="healerfantasy.Items.Amulets.TheHeartOfLight"/>.
///
/// On its own this effect does nothing. Each time the game computes
/// the character's stats via <see cref="Character.GetCharacterStats"/>,
/// it checks whether any non-harmful Holy-school effect is currently
/// active on the carrier. If one is found, incoming damage is reduced
/// by <see cref="DamageReductionAmount"/>.
///
/// This effect is permanent for the life of the combat encounter —
/// it is cleared automatically when the character dies
/// (see <see cref="Character"/>.<c>OnDeath</c>).
/// </summary>
public partial class HolyProtectionEffect : CharacterEffect, IConditionalCharacterModifier
{
	/// <summary>
	/// Fraction by which <see cref="CharacterStats.DamageTakenMultiplier"/> is
	/// scaled down when a Holy effect is active. 0.3 = 30% less damage.
	/// </summary>
	public float DamageReductionAmount { get; }

	/// <param name="damageReductionAmount">
	/// Fraction of incoming damage to negate while a Holy effect is active.
	/// 0.3 means the character takes 30% less damage.
	/// </param>
	public HolyProtectionEffect(float damageReductionAmount)
		: base(float.MaxValue, 0f) // Permanent — managed by combat lifetime, not a timer.
	{
		EffectId = "HolyProtection";
		DamageReductionAmount = damageReductionAmount;
	}

	// ── IConditionalCharacterModifier ─────────────────────────────────────────

	/// <summary>
	/// Reduces <see cref="CharacterStats.DamageTakenMultiplier"/> when any
	/// non-harmful Holy-school effect is currently active on <paramref name="character"/>.
	/// Skips itself to avoid a trivial self-detection loop.
	/// </summary>
	public void Modify(CharacterStats stats, Character character)
	{
		var hasHolyEffect = character.GetAllEffects()
			.Any(e => !e.IsHarmful && e.School == SpellSchool.Holy && e.EffectId != EffectId);

		if (hasHolyEffect)
			stats.DamageTakenMultiplier *= 1f - DamageReductionAmount;
	}
}
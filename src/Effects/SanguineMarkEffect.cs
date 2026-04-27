using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// The Blood Prince's Sanguine Mark debuff.
///
/// Brands a party member with cursed blood, dealing damage per second for
/// 12 seconds. The mark is dispellable — but doing so heals the Blood Prince.
/// The heal is proportional to how much duration remains at dispel time,
/// creating a risk/reward tension identical to the TWSTS Consume mechanic:
///
///   • Dispelled immediately → maximum boss heal (<see cref="BossHealOnDispel"/>)
///   • Dispelled at 12 s elapsed → zero boss heal (the mark ran its course)
///
/// In Phase 2, the Blood Prince applies this mark to two targets simultaneously.
/// </summary>
public partial class SanguineMarkEffect : DamageOverTimeEffect
{
	/// <summary>Maximum boss heal when the mark is dispelled immediately after application.</summary>
	public float BossHealOnDispel { get; init; } = 300f;

	/// <summary>
	/// Reference to the Blood Prince — used to apply the dispel-triggered heal.
	/// Must be set by the spell before calling <see cref="Character.ApplyEffect"/>.
	/// </summary>
	public Character Boss { get; init; }

	public SanguineMarkEffect() : base(20f, 12f, 1f)
	{
		EffectId = "SanguineMark";
		School = SpellSchool.Void;
		IsDispellable = true;
		IsHarmful = true;
	}

	/// <summary>
	/// Heal the boss proportionally to remaining duration when dispelled early.
	/// Natural expiry (Remaining ≤ 0) does not trigger any heal.
	/// </summary>
	public override void OnExpired(Character target)
	{
		if (Remaining <= 0f)
			return; // ran to natural completion — no boss healing

		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
			return;

		var healFraction = Remaining / Duration;
		var healAmount = BossHealOnDispel * healFraction;
		Boss.Heal(healAmount);
		Boss.RaiseFloatingCombatText(healAmount, true, (int)School, false);

		GD.Print($"[BloodPrince] Sanguine Mark dispelled on {target.CharacterName} " +
		         $"after {Duration - Remaining:F1}s — Blood Prince healed for {healAmount:F0}.");
	}
}

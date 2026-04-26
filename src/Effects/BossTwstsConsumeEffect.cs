using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// That Which Swallowed the Stars' "Consume" debuff.
///
/// Deals escalating void damage over 30 seconds — ticks start small and grow
/// larger as the effect runs its course, simulating being slowly devoured.
/// Damage ramps linearly from <see cref="MinDamagePerTick"/> at application to
/// <see cref="MaxDamagePerTick"/> at the 30-second mark.
///
/// Dispel interaction
/// ──────────────────
/// The debuff is dispellable, but doing so heals the boss.  The heal is
/// proportional to how much duration remains at the moment of dispel:
///
///   • Dispelled at  0 s elapsed → maximum heal (<see cref="BossHealOnDispel"/>)
///   • Dispelled at 30 s elapsed → zero heal (the DoT ran to completion)
///
/// This forces a balancing act: dispel too early and the boss is healed
/// significantly; wait too long and the escalating damage threatens to kill
/// the party member.
///
/// If the effect expires naturally (full 30 s) no healing occurs.
/// </summary>
public partial class BossTwstsConsumeEffect : CharacterEffect
{
	/// <summary>Damage per tick at the very start of the debuff.</summary>
	public float MinDamagePerTick { get; init; } = 4f;

	/// <summary>Damage per tick at the end of the full 30-second duration.</summary>
	public float MaxDamagePerTick { get; init; } = 22f;

	/// <summary>Maximum boss heal when the DoT is dispelled immediately after application.</summary>
	public float BossHealOnDispel { get; init; } = 600f;

	/// <summary>
	/// Reference to the boss — used to apply the dispel-triggered heal.
	/// Must be set by the spell before calling <see cref="Character.ApplyEffect"/>.
	/// </summary>
	public Character Boss { get; init; }

	public BossTwstsConsumeEffect() : base(30f, 1f)
	{
		EffectId = "TwstsConsume";
		School = SpellSchool.Void;
		IsDispellable = true;
		IsHarmful = true;
		Icon = Icon;
	}

	protected override void OnTick(Character target)
	{
		// Linear ramp: 0 s elapsed → MinDamagePerTick, full duration elapsed → MaxDamagePerTick.
		var elapsed = Duration - Remaining;
		var t = Mathf.Clamp(elapsed / Duration, 0f, 1f);
		var damage = Mathf.Lerp(MinDamagePerTick, MaxDamagePerTick, t);

		target.TakeDamage(damage);
		target.RaiseFloatingCombatText(damage, false, (int)School, false);
	}

	/// <summary>
	/// Called on natural expiry <em>and</em> on dispel.
	/// We distinguish the two cases by checking <see cref="CharacterEffect.Remaining"/>:
	/// if it is still positive the effect was cut short by Dispel, so we heal
	/// the boss by an amount proportional to the remaining fraction.
	/// </summary>
	public override void OnExpired(Character target)
	{
		if (Remaining <= 0f)
			return; // ran to natural completion — no boss healing

		if (Boss == null || !IsInstanceValid(Boss) || !Boss.IsAlive)
			return;

		// More remaining duration = earlier dispel = more healing for the boss.
		var healFraction = Remaining / Duration;
		var healAmount = BossHealOnDispel * healFraction;
		Boss.Heal(healAmount);

		GD.Print($"[TWSTS] Consume dispelled on {target.CharacterName} after " +
		         $"{Duration - Remaining:F1}s — boss healed for {healAmount:F0}.");
	}
}
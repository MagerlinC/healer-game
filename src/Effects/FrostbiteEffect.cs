using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;

namespace healerfantasy.Effects;

/// <summary>
/// Queen of the Frozen Wastes — Frostbite.
///
/// An infinite-duration debuff applied to the healer at the start of the fight.
/// Deals <see cref="DamagePerStack"/> damage per second per stack.
///
/// Stack rules (managed externally by <see cref="QueenOfTheFrozenWastes"/>):
/// • Gains 1 stack every second the healer stands still.
/// • Loses 1 stack (minimum 1) every second the healer is moving.
///
/// Not dispellable — this is a permanent fight mechanic.
/// Re-application leaves the effect completely unchanged; stacks are managed
/// via <see cref="GainStack"/> and <see cref="LoseStack"/>.
/// </summary>
public partial class FrostbiteEffect : CharacterEffect
{
	public const float DamagePerStack = 5f;

	public FrostbiteEffect()
		: base(GameConstants.InfiniteDuration, 1f)
	{
		EffectId       = "Frostbite";
		MaximumStacks  = 99; // effectively uncapped
		CurrentStacks  = 1;
		IsHarmful      = true;
		IsDispellable  = false;
		School         = SpellSchool.Generic;
	}

	// ── stack management ──────────────────────────────────────────────────────

	/// <summary>
	/// Adds one Frostbite stack.
	/// Called each second the healer is standing still.
	/// </summary>
	public void GainStack() => CurrentStacks++;

	/// <summary>
	/// Removes one Frostbite stack, down to a minimum of 1.
	/// Called each second the healer is moving.
	/// </summary>
	public void LoseStack()
	{
		if (CurrentStacks > 1)
			CurrentStacks--;
	}

	// ── CharacterEffect overrides ─────────────────────────────────────────────

	/// <summary>
	/// Re-application is a no-op — stacks and the infinite timer are managed
	/// externally by <see cref="QueenOfTheFrozenWastes"/>.
	/// </summary>
	public override void OnReapplied(Character target, CharacterEffect newEffect) { }

	protected override void OnTick(Character target)
	{
		var damage = DamagePerStack * CurrentStacks;
		target.TakeDamage(damage);
		target.RaiseFloatingCombatText(damage, false, (int)School, false);

		if (SourceCharacterName == null) return;

		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp   = Time.GetTicksMsec() / 1000.0,
			SourceName  = SourceCharacterName,
			TargetName  = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount      = damage,
			Description = Description,
			Type        = CombatEventType.Damage,
			IsCrit      = false
		});
	}
}

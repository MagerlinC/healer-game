using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// Infinite, ever-escalating buff applied by Void's Embrace.
///
/// Each cast adds a stack with no maximum — every stack increases haste and
/// void damage, but also drains more health per second. The only way this
/// ends is death, or by removing the effect externally.
///
/// Implements <see cref="ICharacterModifier"/> (stat snapshot integration) and
/// <see cref="ISpellModifier"/> (spell-calculation integration).
/// Health drain is handled via <see cref="OnTick"/> rather than
/// <see cref="ICharacterModifier.Modify"/> because it is a per-second side
/// effect, not a flat stat bonus.
/// </summary>
public partial class VoidsEmbraceEffect : CharacterEffect, ISpellModifier
{
	float _healthDrainPerStack;
	float _hastePerStack;
	float _voidDamageIncreasePerStack;

	public VoidsEmbraceEffect(
		float healthDrainPerStack,
		float hastePerStack,
		float voidDamageIncreasePerStack
	) : base(GameConstants.InfiniteDuration, 1f) // 1 s tick interval for health drain
	{
		EffectId = "VoidsEmbrace";
		_healthDrainPerStack = healthDrainPerStack;
		_hastePerStack = hastePerStack;
		_voidDamageIncreasePerStack = voidDamageIncreasePerStack;
	}

	// ── per-second tick: gain a stack then drain health ─────────────────────

	/// <summary>
	/// Every second: gains one stack, then drains <c>_healthDrainPerStack × CurrentStacks</c>
	/// health. Stacks accumulate automatically with no maximum — "this embrace ends in death."
	/// </summary>
	protected override void OnTick(Character target)
	{
		CurrentStacks++;
		var drain = _healthDrainPerStack * CurrentStacks;
		target.TakeDamage(drain);
		target.RaiseFloatingCombatText(drain, false, (int)School, false);

		if (SourceCharacterName == null) return;

		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = SourceCharacterName,
			TargetName = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount = drain,
			Description = Description,
			Type = CombatEventType.Damage,
			IsCrit = false
		});
	}


	// ── ISpellModifier ────────────────────────────────────────────────────────

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext context)
	{
	}

	/// <summary>
	/// Scales both bonuses by <see cref="CharacterEffect.CurrentStacks"/> so
	/// each re-cast of the spell compounds the power (and the danger).
	/// </summary>
	public void OnCalculate(SpellContext context)
	{
		context.CasterStats.IncreasedHaste += _hastePerStack * CurrentStacks;
		context.CasterStats.SpellSchoolIncreasedDamage[SpellSchool.Void] += _voidDamageIncreasePerStack * CurrentStacks;
	}

	public void OnAfterCast(SpellContext context)
	{
	}
}
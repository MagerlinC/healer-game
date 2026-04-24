using System.Collections.Generic;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Void;

/// <summary>
/// Direct void damage spells resonate with active damage-over-time effects,
/// consuming all DoTs on the target to instantly deal all the damage they
/// would have dealt for their remaining duration.
///
/// Creates a powerful DoT → burst combo: apply Decay (or any other DoT) first,
/// then follow up with Shadow Bolt to instantly detonate them.
/// Only applies to instant-damage (non-Duration) void spells.
/// </summary>
public class VoidResonanceTalent : ISpellModifier
{
	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		// Trigger off of Shadow bolt
		if (ctx.Spell.Name != ShadowBoltSpell.SpellName) return;

		var target = ctx.Target;
		if (target == null) return;

		// Collect all active DoTs on the target.
		var dots = new List<DamageOverTimeEffect>();
		foreach (var effect in target.GetAllEffects())
		{
			if (effect is DamageOverTimeEffect dot)
				dots.Add(dot);
		}

		if (dots.Count == 0) return;

		// For each DoT, calculate the damage it would deal over its remaining
		// duration (remaining ticks × damage per tick), then remove it.
		var burst = 0f;
		foreach (var dot in dots)
		{
			// Remaining / TickInterval gives fractional ticks left.
			// We use the full float so a DoT that just ticked still contributes
			// proportional damage rather than rounding down to zero.
			burst += dot.Remaining / dot.TickInterval * dot.DamagePerTick;
			target.RemoveEffect(dot.EffectId);
		}

		ctx.FinalValue += burst;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
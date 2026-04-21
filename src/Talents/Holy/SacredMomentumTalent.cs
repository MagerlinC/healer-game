using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Holy;

/// <summary>
/// Holy spells gain a 15% bonus to their final value when targeting an ally or
/// enemy that is below half their maximum health. Rewards focusing on the most
/// critical targets and reinforces the "saving the day" fantasy of the healer.
/// </summary>
public class SacredMomentumTalent : ISpellModifier
{
	const float Bonus = 1.15f;
	const float HealthThreshold = 0.50f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Holy) return;
		if (ctx.Target == null) return;
		if (ctx.Target.CurrentHealth >= ctx.Target.MaxHealth * HealthThreshold) return;
		ctx.FinalValue *= Bonus;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
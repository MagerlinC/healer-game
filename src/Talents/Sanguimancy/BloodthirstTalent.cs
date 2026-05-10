using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Sanguimancy;

/// <summary>
/// Sanguimancy spells are 25 % more effective — both their damage output and
/// their healing output are amplified.
///
/// This is the school's core damage/healing multiplier, rewarding players who
/// build into the blood magic identity with a flat potency bonus.
/// </summary>
public class BloodthirstTalent : ISpellModifier
{
	const float Bonus = 0.25f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Sanguimancy)) return;
		ctx.FinalValue *= 1f + Bonus;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
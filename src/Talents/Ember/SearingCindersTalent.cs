using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Ember;

/// <summary>
/// Amplifies all Ember-tagged damage spells by 20%.
/// Pairs well with Cinder for a more offensively-oriented Ember build.
/// </summary>
public class SearingCindersTalent : ISpellModifier
{
	const float Bonus = 1.20f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Ember)) return;
		if (!ctx.Spell.Tags.HasFlag(SpellTags.Damage)) return;
		ctx.FinalValue *= Bonus;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
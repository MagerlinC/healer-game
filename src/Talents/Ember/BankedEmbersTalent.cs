using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Ember;

/// <summary>
/// Amplifies all Ember-tagged healing spells by 20%.
/// The Ember school's healing equivalent of Umbral Specialist — a simple but
/// reliable throughput increase for HoT-focused builds.
/// </summary>
public class BankedEmbersTalent : ISpellModifier
{
	const float Bonus = 1.20f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Ember)) return;
		if (!ctx.Spell.Tags.HasFlag(SpellTags.Healing)) return;
		ctx.FinalValue *= Bonus;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
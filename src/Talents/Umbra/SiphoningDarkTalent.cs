using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Umbra;

/// <summary>
/// Each time a Umbra damage spell lands, the caster siphons 1 mana from the
/// umbral. Provides passive mana sustain for Umbra-heavy builds and rewards
/// keeping DoTs active on the enemy.
/// </summary>
public class SiphoningDarkTalent : ISpellModifier
{
	const float ManaPerCast = 1f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{

		if (!ctx.IsSpellOfSchool(SpellSchool.Umbra)) return;
		if (!ctx.Tags.HasFlag(SpellTags.Damage)) return;
		ctx.Caster.RestoreMana(ManaPerCast);
	}
}
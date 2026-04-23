using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// Peering into the flow of time amplifies chronomantic healing.
/// All Chronomancy school healing effects (Time Loop's delayed heal,
/// Rewind's restoration, Temporal Ward's shield, Temporal Echo) are
/// more effective with this talent.
///
/// Doubles down on Chronomancy's proactive, anticipatory healing style —
/// if you pre-place your temporal safety nets correctly, they pay off more.
/// </summary>
public class FutureSightTalent : ISpellModifier
{
	const float BonusMultiplier = 1.20f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Chronomancy) return;
		if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;

		ctx.FinalValue *= BonusMultiplier;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
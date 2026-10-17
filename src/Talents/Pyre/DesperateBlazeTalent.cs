using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Pyre;

/// <summary>
/// The lower the caster's own light burns, the fiercer their Pyre spells
/// blaze — up to 40 % more effective at the brink of death, scaling linearly
/// with the fraction of light the caster is missing.
///
/// The aggressive Pyre capstone: rewards riding low on light and leaning
/// into the self-sacrifice loop. The cost is paid before the pipeline runs,
/// so the light burned by the spell itself already counts toward the bonus.
/// </summary>
public class DesperateBlazeTalent : ISpellModifier
{
	const float MaxBonus = 0.40f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Pyre)) return;
		if (ctx.Caster.MaxHealth <= 0f) return;

		var missingFraction = 1f - ctx.Caster.CurrentHealth / ctx.Caster.MaxHealth;
		if (missingFraction <= 0f) return;

		ctx.FinalValue *= 1f + MaxBonus * missingFraction;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}

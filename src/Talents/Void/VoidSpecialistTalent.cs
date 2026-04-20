using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

/// <summary>
/// Increases the output of spells tagged with Fire, Cold, or Lightning by 20%.
///
/// Applies as an <see cref="ISpellModifier"/> during the OnCalculate phase,
/// so it stacks correctly with stat-based multipliers that run before it.
/// </summary>
public class VoidSpecialistTalent : ISpellModifier
{
	const SpellTags VoidMask = SpellTags.Void;
	const float Bonus = 1.20f; // +20 %

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		// Applies if the spell carries ANY elemental tag.
		if ((ctx.Tags & VoidMask) == SpellTags.None) return;
		ctx.FinalValue *= Bonus;
	}

	public void OnAfterCast(SpellContext ctx)
	{
	}
}
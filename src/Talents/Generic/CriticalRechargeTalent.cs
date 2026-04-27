using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

public class CriticalRechargeTalent : ISpellModifier
{

	const float ManaRestoredOnCrit = 10f;

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;
	public void OnBeforeCast(SpellContext context)
	{
	}
	public void OnCalculate(SpellContext context)
	{
	}
	public void OnAfterCast(SpellContext context)
	{
		if (!context.Tags.HasFlag(SpellTags.Critical)) return;
		context.Caster.RestoreMana(ManaRestoredOnCrit);
	}
}
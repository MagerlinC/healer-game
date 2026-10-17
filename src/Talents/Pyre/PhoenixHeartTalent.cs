using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Pyre;

/// <summary>
/// When a Pyre spell leaves the caster below 40 % light, the flame refuses
/// to gutter out — the light it burned is immediately restored.
///
/// The defensive Pyre capstone: Pyre spells can still drag the caster down
/// to the threshold, but never further, turning low light into a stable
/// state where Pyre spells are effectively free. The refund uses the
/// adjusted cost (e.g. including The Blood Cane's increase).
/// </summary>
public class PhoenixHeartTalent : ISpellModifier
{
	const float HealthThreshold = 0.40f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Pyre)) return;
		if (ctx.Spell.HealthCost <= 0f) return;
		if (ctx.Caster is not { IsAlive: true } caster || caster.MaxHealth <= 0f) return;

		// The cost has already been paid by the time the pipeline runs.
		if (caster.CurrentHealth / caster.MaxHealth >= HealthThreshold) return;

		caster.Heal(ctx.Spell.HealthCost * ctx.CasterStats.LifeCostMultiplier);
	}
}

using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Nature;

/// <summary>
/// Nature's healing energy thrives in healthy hosts.
/// Heal-over-time spells tick for more when the target is already in good
/// health — rewarding proactive maintenance healing over reactive scrambling.
///
/// Incentivises keeping HoTs active on full or near-full targets rather
/// than waiting for health to drop before casting.
/// </summary>
public class OvergrowthTalent : ISpellModifier
{
	const float BonusMultiplier = 1.20f;

	/// <summary>Health fraction above which the bonus applies. 0.75 = above 75% health.</summary>
	const float HealthThreshold = 0.75f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
		if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;
		if (!ctx.Tags.HasFlag(SpellTags.Duration)) return;
		if (!ctx.Tags.HasFlag(SpellTags.Nature)) return;

		// For group spells (Wild Growth), check whether the primary target
		// is healthy. For single-target HoTs, check that target directly.
		var target = ctx.Target;
		if (target == null) return;

		var healthFraction = target.MaxHealth > 0f
			? target.CurrentHealth / target.MaxHealth
			: 0f;

		if (healthFraction >= HealthThreshold)
			ctx.FinalValue *= BonusMultiplier;
	}
	public void OnAfterCast(SpellContext context)
	{
	}
}
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Sanguimancy;

/// <summary>
/// After casting a Sanguimancy spell that costs health, the caster's body
/// fights back — recovering 40 % of the health cost as a heal-over-time
/// over 5 seconds (5 ticks of 8 % each).
///
/// Softens the self-sacrifice loop without eliminating the risk entirely:
/// the caster still takes the full upfront cost, but slowly regenerates
/// a portion of what was spent.
/// </summary>
public class CrimsonReboundTalent : ISpellModifier
{
	const float RecoveryFraction = 0.40f;
	const float Duration = 5f;
	const float TickInterval = 1f;

	public Texture2D EffectIcon { get; set; }
	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}
	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		if (!ctx.IsSpellOfSchool(SpellSchool.Sanguimancy)) return;
		if (ctx.Spell.HealthCost <= 0f) return;

		var healPerTick = ctx.Spell.HealthCost * RecoveryFraction / (Duration / TickInterval);

		ctx.Caster.ApplyEffect(new HealOverTimeEffect(healPerTick, Duration, TickInterval)
		{
			Icon = EffectIcon,
			School = SpellSchool.Sanguimancy,
			SourceCharacterName = ctx.Caster.CharacterName,
			AbilityName = "Crimson Rebound",
			Description = "Recovering health spent on blood magic."
		});
	}
}
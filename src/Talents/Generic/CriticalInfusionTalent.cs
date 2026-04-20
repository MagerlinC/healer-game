using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

/// <summary>
/// When the caster scores a critical hit with any spell, they gain the
/// <see cref="Effects.CriticalInfusionBuff"/> — a 10-second buff that boosts the next
/// damage spell's output by 30%, then consumes itself.
///
/// Implemented as an <see cref="ISpellModifier"/> that reacts in OnAfterCast
/// (after the crit roll has already been applied to FinalValue and the
/// <see cref="SpellTags.Critical"/> tag has been set by the pipeline).
/// </summary>
public class CriticalInfusionTalent : ISpellModifier
{
	const float BuffDuration = 10f; // seconds

	/// <summary>
	/// Icon shown on the buff indicator. Set by <see cref="TalentRegistry"/>
	/// when constructing the talent so the effect displays the talent's own icon
	/// rather than the triggering spell's icon.
	/// </summary>
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
		if (!ctx.Tags.HasFlag(SpellTags.Critical)) return;

		// Grant (or refresh) the buff on the caster.
		ctx.Caster.ApplyEffect(new Effects.CriticalInfusionBuff(BuffDuration)
		{
			Icon = EffectIcon
		});
	}
}
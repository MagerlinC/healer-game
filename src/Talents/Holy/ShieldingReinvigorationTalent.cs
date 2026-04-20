using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

/// <summary>
/// After a healing spell lands, each healed target gains a temporary shield
/// equal to 20% of the healing done, lasting 5 seconds.
///
/// The shield is applied via <see cref="Effects.ShieldEffect"/> which integrates with
/// the existing <see cref="CharacterEffect"/> system for UI display and expiry.
/// Applying the same effect to a target while one is already active refreshes
/// the duration and amount rather than stacking.
/// </summary>
public class ShieldingReinvigorationTalent : ISpellModifier
{
	const float ShieldFraction = 0.30f; // 30% of healing done
	const float ShieldDuration = 5f; // seconds

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
		if (!ctx.Tags.HasFlag(SpellTags.Healing)) return;

		var shieldAmount = ctx.FinalValue * ShieldFraction;

		foreach (var target in ctx.Targets)
		{
			if (!target.IsAlive) continue;
			target.ApplyEffect(new ShieldEffect(shieldAmount, ShieldDuration)
			{
				Icon = EffectIcon // talent icon, not the triggering spell's icon
			});
		}
	}
}
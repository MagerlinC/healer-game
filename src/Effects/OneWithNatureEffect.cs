using System.Linq;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class OneWithNatureEffect : CharacterEffect, ISpellModifier, ICharacterModifier
{

	//Become one with nature for {BuffDuration:F0}s, making all nature spells instant cast and increasing their healing by 20%. Directly healing a target causes all beneficial nature effects on that target to refresh their duration.
	public OneWithNatureEffect(float duration, float tickInterval = 0) : base(duration, tickInterval)
	{
		EffectId = "OneWithNature";
	}

	public void Modify(CharacterStats stats)
	{
		stats.NextCastIsInstant = true;
	}

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;
	public void OnBeforeCast(SpellContext context)
	{
	}
	public void OnCalculate(SpellContext context)
	{
		if (context.Spell.School == SpellSchool.Nature && context.Spell.Tags.HasFlag(SpellTags.Healing))
		{
			context.FinalValue *= 0.20f;
		}
	}
	public void OnAfterCast(SpellContext context)
	{
		if (context.Spell.School == SpellSchool.Nature && context.Spell.Tags.HasFlag(SpellTags.Healing))
		{
			foreach (var target in context.Targets)
			{
				target.RefreshAllPlayerEffects(Character.EffectFilter.FriendlyOnly, SpellSchool.Nature);
			}
		}
	}
}
using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class OneWithTheAshEffect : CharacterEffect, ISpellModifier, ICharacterModifier
{
	float _healingIncrease;

	//Become one with ember for {BuffDuration:F0}s, making all ember spells instant cast and increasing their healing by 20%. Directly healing a target causes all beneficial ember effects on that target to refresh their duration.
	public OneWithTheAshEffect(float duration, float healingIncrease, float tickInterval = 0) : base(duration, tickInterval)
	{
		EffectId = "OneWithTheAsh";
		_healingIncrease = healingIncrease;
		IsUltimateEffect = true;
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
		if (context.Spell.School == SpellSchool.Ember && context.Spell.Tags.HasFlag(SpellTags.Healing))
		{
			context.FinalValue *= _healingIncrease;
		}
	}
	public void OnAfterCast(SpellContext context)
	{
		if (context.Spell.School == SpellSchool.Ember && context.Spell.Tags.HasFlag(SpellTags.Healing))
		{
			foreach (var target in context.Targets)
			{
				target.RefreshAllPlayerEffects(Character.EffectFilter.FriendlyOnly, SpellSchool.Ember);
			}
		}
	}
}
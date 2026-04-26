using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class FutureSightEffect : CharacterEffect, ISpellModifier
{

	public FutureSightEffect(float duration) : base(duration)
	{
		EffectId = "FutureSight";
		Icon = Icon;
		School = SpellSchool.Chronomancy;
		Description = "Your next non-chronomancy spell is instant.";
	}

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;

	public override void OnApplied(Character target)
	{
		target.GetCharacterStats().NextCastIsCrit = true;
	}
	public void OnBeforeCast(SpellContext context)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		// Consume the buff after it has boosted a damage spell.
		if (ctx.Spell.School != SpellSchool.Chronomancy && ctx.Tags.HasFlag(SpellTags.Damage))
			ctx.Caster.RemoveEffect(EffectId);
	}
}
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class FutureSightEffect : CharacterEffect, ISpellModifier, ICharacterModifier
{

	public FutureSightEffect(float duration) : base(duration)
	{
		EffectId = "FutureSight";
		Icon = Icon;
		School = SpellSchool.Chronomancy;
		Description = "Your next non-Chronomancy spell will crit.";
	}

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;

	// ICharacterModifier: injects NextCastIsCrit into the stats snapshot so
	// the spell pipeline picks it up correctly at cast time.
	public void Modify(CharacterStats stats)
	{
		stats.NextCastIsCrit = true;
	}

	public override void OnApplied(Character target)
	{
	}

	public void OnBeforeCast(SpellContext context)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		// Consume the buff: removing the effect stops Modify() from running,
		// so NextCastIsCrit will be false on the following cast.
		ctx.Caster.RemoveEffect(EffectId);
	}
}
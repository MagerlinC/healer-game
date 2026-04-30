using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

public partial class TemporalMomentumEffect : CharacterEffect, ISpellModifier, ICharacterModifier
{

	public TemporalMomentumEffect(float duration) : base(duration)
	{
		EffectId = "TemporalMomentum";
		Icon = Icon;
		School = SpellSchool.Chronomancy;
		Description = "Your next non-Chronomancy spell is instant.";
	}

	public ModifierPriority Priority { get; } = ModifierPriority.BASE;

	// ICharacterModifier: injects NextCastIsInstant into the stats snapshot so
	// Player.cs picks it up correctly when deciding whether to skip the cast bar.
	public void Modify(CharacterStats stats)
	{
		stats.NextCastIsInstant = true;
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
		// Only consume the buff when a non-Chronomancy spell is cast —
		// Chronomancy spells are excluded from the instant-cast benefit in
		// Player.cs, so casting one should not spend the charge.
		if (ctx.Spell.School != SpellSchool.Chronomancy)
			ctx.Caster.RemoveEffect(EffectId);
	}
}
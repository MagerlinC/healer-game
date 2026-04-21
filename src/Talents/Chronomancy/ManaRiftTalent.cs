using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// Each Chronomancy spell cast has a 20% chance to tear a brief rift in time,
/// refunding its full mana cost. Provides explosive mana efficiency in lucky
/// moments and rewards building a full Chronomancy spell lineup.
/// </summary>
public class ManaRiftTalent : ISpellModifier
{
	const float RefundChance = 0.20f;

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	public void OnAfterCast(SpellContext ctx)
	{
		if (ctx.Spell.School != SpellSchool.Chronomancy) return;
		if (GD.Randf() < RefundChance)
			ctx.Caster.RestoreMana(ctx.Spell.ManaCost);
	}
}
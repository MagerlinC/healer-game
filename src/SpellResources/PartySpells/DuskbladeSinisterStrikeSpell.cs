using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Duskblade's basic melee attack — fast and frequent.
/// Deals instant physical damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class DuskbladeSinisterStrikeSpell : SpellResource
{
	public float DamageAmount = 15f;

	public DuskbladeSinisterStrikeSpell()
	{
		Name = "Sinister Strike";
		Description = "A quick, precise strike from the Duskblade, out of the half-light.";
		Tags = SpellTags.Damage | SpellTags.Attack;
		ManaCost = 0f;
		CastTime = 0f;
		School = SpellSchool.Generic;
		TargetingType = TargetingType.Enemy;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}
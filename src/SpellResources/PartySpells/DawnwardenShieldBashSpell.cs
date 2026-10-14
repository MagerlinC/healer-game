using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Dawnwarden's basic melee attack.
/// Deals instant physical damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class DawnwardenShieldBashSpell : SpellResource
{
	public float DamageAmount = 20f;

	public DawnwardenShieldBashSpell()
	{
		Name = "Shield Bash";
		Description = "The Dawnwarden crashes their shield into the enemy with full force.";
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
using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

public partial class FlareSpell : SpellResource
{

	[Export] public float DamageAmount = 25f;

	public FlareSpell()
	{
		Name = "Flare";
		Description = $"Deals {DamageAmount} light damage to target enemy.";
		School = SpellSchool.Flame;
		ManaCost = 5f;
		CastTime = 1.2f;
		Tags = SpellTags.Damage | SpellTags.Light;
		School = SpellSchool.Flame;
		TargetingType = TargetingType.Enemy;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}


	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.TakeDamage(ctx.FinalValue);
	}
}
using Godot.Collections;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Starcaller's basic damage spell — slower but hits harder than the melee attacks.
/// Deals instant magic damage to the boss.
/// </summary>
[Godot.GlobalClass]
public partial class StarcallerStarshardSpell : SpellResource
{
	public float DamageAmount = 35f;

	public StarcallerStarshardSpell()
	{
		Name = "Starshard";
		Description = "A shard of cold starlight hurled at the enemy.";
		Tags = SpellTags.Damage | SpellTags.Attack;
		ManaCost = 0f;
		CastTime = 0f;
		School = SpellSchool.Umbra;
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
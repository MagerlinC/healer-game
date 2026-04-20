using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

public partial class BurstOfLightSpell : SpellResource
{

	[Export] public float DamageAmount = 25f;

	public BurstOfLightSpell()
	{
		Name = "Burst of Light";
		Description = $"Deals {DamageAmount} light damage to target enemy.";
		School = SpellSchool.Holy;
		ManaCost = 5f;
		CastTime = 1.5f;
		Tags = SpellTags.Damage | SpellTags.Light;
		School = SpellSchool.Holy;
		TargetType = TargetType.Enemy;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer6.png");
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
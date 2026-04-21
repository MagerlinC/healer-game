using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class TouchOfLightSpell : SpellResource
{
	[Export] public float HealAmount = 25f;

	public TouchOfLightSpell()
	{
		Name = "Touch of Light";
		Description = $"Restores {HealAmount} health to the target.";
		ManaCost = 5f;
		CastTime = 1.5f;
		Tags = SpellTags.Healing;
		School = SpellSchool.Holy;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/holy/touch-of-light.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.Heal(ctx.FinalValue);
	}
}
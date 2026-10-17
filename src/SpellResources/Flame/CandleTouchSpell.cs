using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

[GlobalClass]
public partial class CandleTouchSpell : SpellResource
{
	[Export] public float HealAmount = 30f;

	public CandleTouchSpell()
	{
		Name = "Candle Touch";
		Description = $"Restores {HealAmount} light to the target.";
		ManaCost = 6f;
		CastTime = 1.5f;
		Tags = SpellTags.Healing;
		School = SpellSchool.Flame;
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
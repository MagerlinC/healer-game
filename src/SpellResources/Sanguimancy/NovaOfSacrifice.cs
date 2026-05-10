using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Sanguimancy;

public partial class NovaOfSacrifice : SpellResource
{

	float hpCost = 25f;
	float conversionFactor = 2f;
	public NovaOfSacrifice()
	{
		Name = "Nova of Sacrifice";
		Description =
			$"Erupt in a nova of blood, sacrificing {hpCost} health to apply a blood shield to all allies lasting 8 seconds. The shield absorbs damage equal to twice the health sacrificed";
		ManaCost = 0f;
		HealthCost = hpCost;
		CastTime = 2.0f;
		Cooldown = 8f;
		School = SpellSchool.Sanguimancy;
		Tags = SpellTags.Healing | SpellTags.Sanguimancy;
		RequiredSchoolPoints = 2;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/nova-of-sacrifice.png");
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character { IsAlive: true } c)
				targets.Add(c);
		return targets;
	}

	public override float GetBaseValue()
	{
		return hpCost * conversionFactor;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.AddShield(ctx.FinalValue);
	}
}
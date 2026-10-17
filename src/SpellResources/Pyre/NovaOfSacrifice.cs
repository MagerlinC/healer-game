using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Pyre;

public partial class NovaOfSacrifice : SpellResource
{

	float hpCost = 25f;
	float conversionFactor = 2f;
	float shieldDuration = 8f;
	public NovaOfSacrifice()
	{
		Name = "Nova of Sacrifice";
		Description =
			$"Erupt in a nova of blood, burning {hpCost} of your own light to apply a blood shield to all allies lasting {shieldDuration} seconds. The shield absorbs damage equal to twice the light burned.";
		ManaCost = 0f;
		HealthCost = hpCost;
		CastTime = 2.0f;
		Cooldown = 8f;
		School = SpellSchool.Pyre;
		TargetingType = TargetingType.None;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 2;
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
			target.ApplyEffect(new ShieldEffect("NovaOfSacrifice", ctx.FinalValue, shieldDuration)
			{
				Icon = ctx.Spell.Icon,
				School = School,
				SourceCharacterName = ctx.Caster.CharacterName,
				AbilityName = Name,
				Description = Description
			});
	}
}
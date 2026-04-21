using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Chronomancy;

/// <summary>
/// Accelerates the flow of time around the entire party, granting each member
/// a +20% cast-speed buff for a short duration via <see cref="Effects.TimeWarpEffect"/>.
/// </summary>
[GlobalClass]
public partial class TimeWarpSpell : SpellResource
{
	[Export] public float BuffDuration = 6f;
	[Export] public float CastSpeedBonus = 0.20f;

	public TimeWarpSpell()
	{
		Name = "Time Warp";
		Description =
			$"Accelerates time for the whole party, increasing cast speed by {(int)(CastSpeedBonus * 100)}% for {BuffDuration}s.";
		ManaCost = 15f;
		CastTime = 0.0f;
		Cooldown = 10f;
		School = SpellSchool.Chronomancy;
		Tags = SpellTags.Duration | SpellTags.GroupSpell;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/chronomancy/time-warp.png");
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character { IsAlive: true } c)
				targets.Add(c);
		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.ApplyEffect(new Effects.TimeWarpEffect(BuffDuration, CastSpeedBonus)
			{
				Icon = Icon,
				SourceCharacterName = ctx.Caster.CharacterName,
				AbilityName = Name
			});
	}
}
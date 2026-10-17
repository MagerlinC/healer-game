using System.Collections.Generic;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Wick;

/// <summary>
/// Weaves a temporal barrier around the entire party, surrounding each member
/// with a brief time-locked shield that absorbs incoming damage before it can
/// reach their health.
///
/// Unlike Lantern Ward (single-target Flame shield), Wax Seal applies to
/// everyone at once — invaluable before a boss's wide-area attack that would
/// otherwise force frantic single-target healing.
/// </summary>
[GlobalClass]
public partial class WaxSealSpell : SpellResource
{
	[Export] public float ShieldAmount = 20f;
	[Export] public float ShieldDuration = 5f;

	public WaxSealSpell()
	{
		Name = "Wax Seal";
		Description =
			$"Surround all party members in a seal of hardened wax, absorbing up to {ShieldAmount} damage each for {ShieldDuration}s.";
		ManaCost = 15f;
		CastTime = 1.5f;
		Cooldown = 8f;
		School = SpellSchool.Wick;
		Tags = SpellTags.Healing | SpellTags.Duration | SpellTags.GroupSpell;
		RequiredSchoolPoints = 0;
		TargetingType = TargetingType.None;
	}

	public override float GetBaseValue()
	{
		return ShieldAmount;
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
		{
			target.ApplyEffect(new ShieldEffect("WaxSeal", ctx.FinalValue, ShieldDuration)
			{
				EffectId = "WaxSeal",
				Icon = ctx.Spell.Icon,
				School = School,
				SourceCharacterName = ctx.Caster.CharacterName,
				AbilityName = Name,
				Description = Description
			});
		}
	}
}
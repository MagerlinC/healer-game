using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Heals every member of the "party" group for a fixed amount.
/// The explicit target is ignored — all party members are resolved via
/// <see cref="ResolveTargets"/>, so the modifier pipeline (including
/// <see cref="ShieldingReinvigorationTalent"/>) operates on the full group.
/// </summary>
[GlobalClass]
public partial class GroupHealSpellResource : SpellResource
{
	[Export] public float HealAmount = 25f;

	public GroupHealSpellResource()
	{
		Name = "Wave of Vitality";
		Description = $"Restores {HealAmount} health to all party members.";
		ManaCost = 20f;
		CastTime = 2f;
		Tags = SpellTags.Healing | SpellTags.GroupSpell;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/healer/healer2.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
			if (node is Character c && c.IsAlive)
				targets.Add(c);
		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.Heal(ctx.FinalValue);
	}
}
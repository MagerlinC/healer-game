using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Heals every member of the "party" group for a fixed amount.
/// The explicit target is ignored — all party members are resolved via
/// <see cref="ResolveTargets"/>, so the modifier pipeline (including
/// </summary>
[GlobalClass]
public partial class WaveOfIncandescenceSpell : SpellResource
{
	[Export] public float HealAmount = 25f;

	public WaveOfIncandescenceSpell()
	{
		Name = "Wave of Incandescence";
		Description = $"Restores {HealAmount} health to all party members.";
		ManaCost = 20f;
		CastTime = 2f;
		Cooldown = 8f;
		School = SpellSchool.Holy;
		Tags = SpellTags.Healing | SpellTags.GroupSpell;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/holy/wave-of-incandescence.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
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
			target.Heal(ctx.FinalValue);
	}
}
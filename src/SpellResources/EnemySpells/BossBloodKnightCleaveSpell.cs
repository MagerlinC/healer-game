using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Blood Knight's Crimson Strike — a sweeping cleave that hits both frontliners.
///
/// Targets the Templar and the Assassin simultaneously (the two melee party members).
/// Falls back to any alive party members if either is dead.
/// </summary>
[GlobalClass]
public partial class BossBloodKnightCleaveSpell : SpellResource
{
	public float DamageAmount = 28f;

	public BossBloodKnightCleaveSpell()
	{
		Name = "Crimson Strike";
		Description = "A wide crimson slash that cleaves through the frontline, striking both melee fighters.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	/// <summary>
	/// Targets the Templar and Assassin by name. Falls back to the full living
	/// party if neither frontliner can be found.
	/// </summary>
	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		var targets = new List<Character>();
		foreach (var node in caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character c || !c.IsAlive) continue;
			if (c.CharacterName == GameConstants.TemplarName ||
			    c.CharacterName == GameConstants.AssassinName)
				targets.Add(c);
		}

		// If neither frontliner is alive, fall back to anyone still standing.
		if (targets.Count == 0)
			foreach (var node in caster.GetTree().GetNodesInGroup("party"))
				if (node is Character c && c.IsAlive)
					targets.Add(c);

		return targets;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

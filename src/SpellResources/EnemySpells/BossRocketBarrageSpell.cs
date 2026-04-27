using System.Collections.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Mecha Golem's AoE bombardment — Rocket Barrage.
/// Launches a volley of rockets that pepper the entire party
/// for moderate damage each. No warning — hit everyone at once.
/// </summary>
[Godot.GlobalClass]
public partial class BossRocketBarrageSpell : SpellResource
{
	public float DamageAmount = 30f;

	public BossRocketBarrageSpell()
	{
		Name        = "Rocket Barrage";
		Description = "The Mecha Golem launches a volley of rockets that bombard all party members simultaneously.";
		Tags        = SpellTags.Damage;
		ManaCost    = 0f;
		CastTime    = 0f;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	/// <summary>Targets every alive party member.</summary>
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
			target.TakeDamage(ctx.FinalValue);
	}
}

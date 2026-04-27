using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Astral Twins' telegraphed AoE — Celestial Convergence.
/// Both twins channel simultaneously; after a 3-second wind-up the combined
/// starlight detonates across the entire party. Deflectable.
/// </summary>
[GlobalClass]
public partial class BossCelestialConvergenceSpell : SpellResource
{
	public float DamageAmount = 80f;

	public BossCelestialConvergenceSpell()
	{
		Name = "Celestial Convergence";
		Description =
			$"The twins channel their power in unison, unleashing a blinding burst of starlight that engulfs the whole party, dealing {DamageAmount} void damage unless deflected.";
		Tags = SpellTags.Damage;
		School = SpellSchool.Void;
		ManaCost = 0f;
		CastTime = 0f;
		Parryable = true;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	/// <summary>Targets the entire living party.</summary>
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
using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Nightborne's telegraphed AoE — Umbral Eruption.
/// After a 3.5-second wind-up (during which the boss charges forward),
/// a wave of shadow energy erupts and hits every party member.
/// Can be deflected.
/// </summary>
[GlobalClass]
public partial class BossNightborneUmbralEruptionSpell : SpellResource
{
	public float DamageAmount = 90f;

	public BossNightborneUmbralEruptionSpell()
	{
		Name        = "Umbral Eruption";
		Description = "The Nightborne charges with terrifying speed and erupts in a wave of pure shadow, engulfing the entire party — unless deflected.";
		Tags        = SpellTags.Damage | SpellTags.Void;
		ManaCost    = 0f;
		CastTime    = 0f;
		Parryable   = true;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

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

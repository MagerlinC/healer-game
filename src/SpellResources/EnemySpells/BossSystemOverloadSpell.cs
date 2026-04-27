using System.Collections.Generic;
using healerfantasy.SpellResources.Generic;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Mecha Golem's telegraphed AoE — System Overload.
/// After a 3.5-second wind-up during which the Golem raises its shield
/// and builds up a catastrophic charge, it unleashes a devastating
/// electro-mechanical explosion that hits the entire party for massive
/// damage — unless deflected.
///
/// The parry check is resolved by <see cref="MechaGolem"/> before this
/// spell is ever cast; if deflected, the cast is skipped entirely.
/// </summary>
[Godot.GlobalClass]
public partial class BossSystemOverloadSpell : SpellResource
{
	public float DamageAmount = 70f;

	public BossSystemOverloadSpell()
	{
		Name        = "System Overload";
		Description = "The Mecha Golem overcharges its core and unleashes a catastrophic explosion — unless deflected.";
		Tags        = SpellTags.Damage;
		ManaCost    = 0f;
		CastTime    = 0f;
		Parryable   = true;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

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
		DeflectSpell.PlayDeflectFailedSound(ctx.Caster);
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

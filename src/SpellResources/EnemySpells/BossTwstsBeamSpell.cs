using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// That Which Swallowed the Stars' basic attack — Stellar Beam.
/// A concentrated beam of void-star energy fired at a random party member.
/// Not deflectable — simply a heavy damage hit to heal through.
/// </summary>
[GlobalClass]
public partial class BossTwstsBeamSpell : SpellResource
{
	public float DamageAmount = 75f;

	public BossTwstsBeamSpell()
	{
		Name        = "Stellar Beam";
		Description = "A searing beam of compressed starlight burns through a random party member.";
		Tags        = SpellTags.Damage | SpellTags.Void;
		ManaCost    = 0f;
		CastTime    = 0f;
		Parryable   = false;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

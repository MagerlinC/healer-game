using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Nightborne's ranged spell — Void Lance.
/// A bolt of concentrated void energy hurled at a random party member.
/// </summary>
[GlobalClass]
public partial class BossNightborneVoidLanceSpell : SpellResource
{
	public float DamageAmount = 45f;

	public BossNightborneVoidLanceSpell()
	{
		Name        = "Void Lance";
		Description = "A concentrated spear of void energy that pierces through armour and strikes a random party member.";
		Tags        = SpellTags.Damage | SpellTags.Void;
		ManaCost    = 0f;
		CastTime    = 0f;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Countess's Noble Strike — a swift, elegant melee attack aimed at the tank.
/// Her primary filler attack between spells.
/// </summary>
[GlobalClass]
public partial class BossCountessStrikeSpell : SpellResource
{
	public float DamageAmount = 38f;

	public BossCountessStrikeSpell()
	{
		Name = "Noble Strike";
		Description = "A swift, precise strike from a blade dipped in cursed blood.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 0f;
		EffectType = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Blood Knight's Bloodthirst — a savage melee strike aimed at the tank.
/// Deals straightforward physical damage. The Blood Knight's primary filler attack.
/// </summary>
[GlobalClass]
public partial class BossBloodKnightMeleeSpell : SpellResource
{
	public float DamageAmount = 45f;

	public BossBloodKnightMeleeSpell()
	{
		Name = "Bloodthirst";
		Description = "A savage melee strike fuelled by bloodlust.";
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

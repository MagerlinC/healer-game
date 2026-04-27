using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Countess's Blood Bolt — a ranged crimson projectile fired at a random party member.
/// </summary>
[GlobalClass]
public partial class BossCountessBloodBoltSpell : SpellResource
{
	public float DamageAmount = 42f;

	public BossCountessBloodBoltSpell()
	{
		Name = "Blood Bolt";
		Description = "A condensed bolt of cursed blood hurled at a random party member.";
		Tags = SpellTags.Damage | SpellTags.Void;
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

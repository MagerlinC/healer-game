using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Nightborne's melee — Shadow Strike.
/// A brutal necrotic slash aimed at the tank.
/// </summary>
[GlobalClass]
public partial class BossNightborneShadowStrikeSpell : SpellResource
{
	public float DamageAmount = 60f;

	public BossNightborneShadowStrikeSpell()
	{
		Name        = "Shadow Strike";
		Description = "The Nightborne lunges forward with a devastating shadow-infused blade, targeting the frontline defender.";
		Tags        = SpellTags.Damage | SpellTags.Void | SpellTags.Attack;
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

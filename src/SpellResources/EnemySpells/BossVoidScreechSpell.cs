using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Skull's ranged attack — Void Screech.
/// Lets out a piercing screech of void energy aimed at a random party member,
/// dealing moderate void damage.
/// </summary>
[Godot.GlobalClass]
public partial class BossVoidScreechSpell : SpellResource
{
	public float DamageAmount = 38f;

	public BossVoidScreechSpell()
	{
		Name        = "Void Screech";
		Description = "A piercing screech of void energy aimed at a party member, dealing void damage.";
		Tags        = SpellTags.Damage | SpellTags.Void;
		ManaCost    = 0f;
		CastTime    = 0f;
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

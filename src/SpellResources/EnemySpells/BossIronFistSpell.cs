using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Mecha Golem's melee auto-attack — Iron Fist.
/// A thunderous mechanical punch aimed at the party's tank,
/// dealing heavy physical damage.
/// </summary>
[Godot.GlobalClass]
public partial class BossIronFistSpell : SpellResource
{
	public float DamageAmount = 55f;

	public BossIronFistSpell()
	{
		Name        = "Iron Fist";
		Description = "A thunderous mechanical punch aimed at the party's frontline, dealing heavy physical damage.";
		Tags        = SpellTags.Damage | SpellTags.Attack;
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

using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Icy Strike.
/// A swift melee auto-attack directed at the tank (Templar).
/// Deals instant physical damage; no cast time or mana cost.
/// </summary>
[Godot.GlobalClass]
public partial class BossQueenIcyStrikeSpell : SpellResource
{
	public float DamageAmount = 25f;

	public BossQueenIcyStrikeSpell()
	{
		Name        = "Icy Strike";
		Description = "A swift blow from the Queen's ice-forged gauntlet.";
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

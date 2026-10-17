using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Scalding Touch.
/// A swift melee auto-attack directed at the tank (Dawnwarden).
/// Deals instant physical damage; no cast time or mana cost.
/// </summary>
[Godot.GlobalClass]
public partial class BossQueenScaldingTouchSpell : SpellResource
{
	public float DamageAmount = 25f;

	public BossQueenScaldingTouchSpell()
	{
		Name        = "Scalding Touch";
		Description = "A searing blow from the Queen's dripping, wax-slick hand.";
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

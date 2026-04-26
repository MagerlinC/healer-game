using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// That Which Swallowed the Stars' melee auto-attack — Void Tendril Strike.
/// Deals instant void-physical damage to its explicit target (the tank,
/// or a random party member if the tank is dead).
/// Damage amount is configured via <see cref="DamageAmount"/>.
/// </summary>
[Godot.GlobalClass]
public partial class BossTwstsMeleeAttackSpell : SpellResource
{
	public float DamageAmount = 30f;

	public BossTwstsMeleeAttackSpell()
	{
		Name        = "Void Tendril Strike";
		Description = "A lashing strike from one of the star-devourer's vast cosmic tendrils.";
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

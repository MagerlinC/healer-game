using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Demon's melee auto-attack — Fel Strike.
/// A savage claw swipe laced with demonic energy aimed at the party's tank.
/// </summary>
[Godot.GlobalClass]
public partial class BossFelStrikeSpell : SpellResource
{
	public float DamageAmount = 45f;

	public BossFelStrikeSpell()
	{
		Name        = "Fel Strike";
		Description = "A savage claw swipe laced with demonic energy, aimed at the party's frontline.";
		Tags        = SpellTags.Damage;
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

using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Demon's ranged attack — Hellfire Bolt.
/// Hurls a concentrated ball of fel fire at a random party member,
/// dealing moderate fire damage.
/// </summary>
[Godot.GlobalClass]
public partial class BossHellfireBoltSpell : SpellResource
{
	public float DamageAmount = 35f;

	public BossHellfireBoltSpell()
	{
		Name        = "Hellfire Bolt";
		Description = "A concentrated ball of fel fire hurled at a party member, dealing fire damage on impact.";
		Tags        = SpellTags.Damage | SpellTags.Nature;
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

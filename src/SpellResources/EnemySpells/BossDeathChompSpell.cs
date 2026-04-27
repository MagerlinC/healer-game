using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Flying Skull's melee auto-attack — Death Chomp.
/// The skull dives in and snaps its jaw shut on the party's tank,
/// dealing heavy void damage.
/// </summary>
[Godot.GlobalClass]
public partial class BossDeathChompSpell : SpellResource
{
	public float DamageAmount = 50f;

	public BossDeathChompSpell()
	{
		Name        = "Death Chomp";
		Description = "The Flying Skull snaps its jaw shut on the party's frontline, dealing heavy void damage.";
		Tags        = SpellTags.Damage | SpellTags.Void | SpellTags.Attack;
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

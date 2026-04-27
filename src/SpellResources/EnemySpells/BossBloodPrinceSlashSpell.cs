using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Blood Prince's Regal Slash — a contemptuous, powerful melee strike at the tank.
/// Hits harder in Phase 2 (the boss sets <see cref="DamageAmount"/> directly).
/// </summary>
[GlobalClass]
public partial class BossBloodPrinceSlashSpell : SpellResource
{
	public float DamageAmount = 45f;

	public BossBloodPrinceSlashSpell()
	{
		Name = "Regal Slash";
		Description = "A contemptuous slash from a blade wreathed in cursed blood.";
		Tags = SpellTags.Damage;
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

using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Astral Twins' melee — Astral Strike.
/// A precise celestial blade strike aimed at the tank.
/// </summary>
[GlobalClass]
public partial class BossAstralStrikeSpell : SpellResource
{
	public float DamageAmount = 50f;

	public BossAstralStrikeSpell()
	{
		Name = "Astral Strike";
		Description = "A razor-sharp strike of condensed starlight aimed at the frontline defender.";
		Tags = SpellTags.Damage | SpellTags.Attack;
		ManaCost = 0f;
		CastTime = 0f;
		TargetingType = TargetingType.Enemy;
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}
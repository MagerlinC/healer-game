using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Astral Twins' ranged spell — Starfall.
/// A bolt of compressed starlight hurled at a random party member.
/// </summary>
[GlobalClass]
public partial class BossAstralStarfallSpell : SpellResource
{
	public float DamageAmount = 40f;

	public BossAstralStarfallSpell()
	{
		Name        = "Starfall";
		Description = "A condensed bolt of starlight streaks toward a random party member.";
		Tags        = SpellTags.Damage;
		ManaCost    = 0f;
		CastTime    = 0f;
		EffectType  = EffectType.Harmful;
	}

	public override float GetBaseValue() => DamageAmount;

	public override void Apply(SpellContext ctx)
	{
		foreach (var target in ctx.Targets)
			target.TakeDamage(ctx.FinalValue);
	}
}

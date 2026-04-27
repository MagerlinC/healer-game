using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Blood Prince's Blood Bolt — a ranged burst of corrupted blood fired at a random party member.
/// </summary>
[GlobalClass]
public partial class BossBloodPrinceBloodBoltSpell : SpellResource
{
	public float DamageAmount = 48f;

	public BossBloodPrinceBloodBoltSpell()
	{
		Name = "Blood Bolt";
		Description = "A pressurised burst of the Prince's own blood, launched at a random target.";
		Tags = SpellTags.Damage | SpellTags.Void;
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

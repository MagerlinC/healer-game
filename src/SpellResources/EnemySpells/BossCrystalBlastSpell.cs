using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Crystal Knight's ranged spell — fires a shard of crystalline cold energy
/// at a random party member, dealing instant damage.
/// Damage amount is configured via <see cref="DamageAmount"/>.
/// </summary>
[Godot.GlobalClass]
public partial class BossCrystalBlastSpell : SpellResource
{
	public float DamageAmount = 15f;

	public BossCrystalBlastSpell()
	{
		Name = "Crystal Blast";
		Description = "A shard of void crystal energy launched at a random target.";
		Tags = SpellTags.Damage | SpellTags.Void;
		ManaCost = 20f;
		CastTime = 0f;
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
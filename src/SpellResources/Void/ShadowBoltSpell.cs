using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Hurls a concentrated bolt of void energy at a single target, dealing
/// immediate burst damage. The Void school's direct-damage counterpart to
/// the DoT-focused Decay.
/// </summary>
[GlobalClass]
public partial class ShadowBoltSpell : SpellResource
{
	[Export] public float DamageAmount = 40f;

	public ShadowBoltSpell()
	{
		Name = "Shadow Bolt";
		Description = $"Launches a bolt of concentrated shadow at the target, dealing {DamageAmount} void damage.";
		ManaCost = 8f;
		CastTime = 1.5f;
		School = SpellSchool.Void;
		Tags = SpellTags.Damage | SpellTags.Void;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>("res://assets/spell-icons/void/shadow-bolt.png");
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.TakeDamage(ctx.FinalValue);
	}
}
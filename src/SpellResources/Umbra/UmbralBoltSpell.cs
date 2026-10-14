using Godot;
using healerfantasy;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Hurls a concentrated bolt of umbral energy at a single target, dealing
/// immediate burst damage. The Umbra school's direct-damage counterpart to
/// the DoT-focused Decay.
/// </summary>
[GlobalClass]
public partial class UmbralBoltSpell : SpellResource
{
	public static readonly string SpellName = "Umbral Bolt";
	[Export] public float DamageAmount = 30f;

	public UmbralBoltSpell()
	{
		Name = SpellName;
		Description = $"Launches a bolt of concentrated dark at the target, dealing {DamageAmount} umbral damage.";
		ManaCost = 10f;
		CastTime = 1.5f;
		School = SpellSchool.Umbra;
		TargetingType = TargetingType.Enemy;
		Tags = SpellTags.Damage;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/shadow-bolt.png");
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
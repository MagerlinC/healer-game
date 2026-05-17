using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

public partial class TouchOfAffliction : SpellResource
{
	public static readonly string SpellName = "Touch of Affliction";
	[Export] public float AddedDamagePerDoT = 15f;
	[Export] public float BaseDamage = 10f;

	public TouchOfAffliction()
	{
		Name = SpellName;
		Description =
			$"Reach into the afflictions of the target dealing {BaseDamage} void damage, plus an additional {AddedDamagePerDoT} per active damage-over-time effect on the target.";
		ManaCost = 10f;
		CastTime = 0.0f;
		School = SpellSchool.Void;
		Cooldown = 6f;
		Tags = SpellTags.Damage;
		RequiredSchoolPoints = 1;
		TargetingType = TargetingType.Enemy;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/touch-of-affliction.png");
	}

	public override float GetBaseValue()
	{
		return BaseDamage;
	}

	/// <summary>
	/// Count active DoT effects on the target and fold the per-DoT bonus into
	/// <see cref="SpellContext.BaseValue"/> so that the full damage flows through
	/// the modifier pipeline, crit roll, and central combat-log recording correctly.
	/// </summary>
	public override void OnAfterTargetsResolved(SpellContext ctx)
	{
		var dotCount = ctx.Target?.GetAllEffects().Count(e => e is DamageOverTimeEffect) ?? 0;
		ctx.BaseValue = BaseDamage + dotCount * AddedDamagePerDoT;
	}

	public override void Apply(SpellContext ctx)
	{
		// ctx.FinalValue already includes BaseDamage + per-DoT bonus,
		// scaled by damage multipliers and crit.
		ctx.Target?.TakeDamage(ctx.FinalValue);
	}
}
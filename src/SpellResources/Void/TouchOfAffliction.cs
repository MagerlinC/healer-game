using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Void;

public partial class TouchOfAffliction : SpellResource
{
	public static readonly string SpellName = "Touch of Affliction";
	[Export] public float AddedDamagePerDoT = 5f;
	[Export] public float BaseDamage = 10f;

	public TouchOfAffliction()
	{
		Name = SpellName;
		Description =
			$"Reach into the afflictions of the target dealing {BaseDamage} void damage, plus an additional {AddedDamagePerDoT} per active damage-over-time effect on the target.";
		ManaCost = 10f;
		CastTime = 0.0f;
		School = SpellSchool.Void;
		Tags = SpellTags.Damage;
		RequiredSchoolPoints = 1;
		EffectType = EffectType.Harmful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "void/touch-of-affliction.png");
	}

	public override float GetBaseValue()
	{
		return BaseDamage;
	}

	public override void Apply(SpellContext ctx)
	{
		var dotsOnTarget = ctx.Target.GetAllEffects().Count(e => e is DamageOverTimeEffect);
		ctx.Target?.TakeDamage(ctx.FinalValue + dotsOnTarget * AddedDamagePerDoT);
	}
}
using System.Linq;
using Godot;
using healerfantasy.Effects;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// A nourishing heal that draws on nature's existing growth energy.
/// Heals the target for <see cref="HealAmount"/> HP normally, but if the
/// target already has a heal-over-time effect active, the heal is amplified
/// by <see cref="HotBonusMultiplier"/> — nature's roots run deeper when
/// the ground is already fertile.
///
/// Pairs naturally with Renewing Bloom: apply the bloom first, then follow
/// up with Nourish to burst-heal a target for significantly more than the
/// base value alone.
/// </summary>
[GlobalClass]
public partial class NourishSpell : SpellResource
{
	[Export] public float HealAmount = 25f;

	/// <summary>
	/// Multiplicative bonus applied when a HoT is active on the target.
	/// 1.30 = 30% more healing.
	/// </summary>
	[Export] public float HotBonusMultiplier = 1.50f;

	public NourishSpell()
	{
		Name = "Nourish";
		Description =
			$"Heals the target for {HealAmount} HP. Heals for {100 * (HotBonusMultiplier - 1f):F0}% more if the target has a nature heal-over-time effect active.";
		ManaCost = 10f;
		CastTime = 2.0f;
		Cooldown = 0f;
		School = SpellSchool.Nature;
		Tags = SpellTags.Healing;
		RequiredSchoolPoints = 1;
		EffectType = EffectType.Helpful;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "nature/swarm-of-locusts.png");
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	/// <summary>
	/// Check for an active nature HoT on the target and, if present, apply the
	/// bonus multiplier to <see cref="SpellContext.BaseValue"/> before the modifier
	/// pipeline runs. This ensures the full amplified value flows through healing
	/// multipliers, crit, and the central combat-log correctly.
	/// </summary>
	public override void OnAfterTargetsResolved(SpellContext ctx)
	{
		if (ctx.Target == null) return;

		var hasHot = ctx.Target.GetAllEffects().Any(e => e.School == SpellSchool.Nature && e is HealOverTimeEffect);
		if (hasHot)
			ctx.BaseValue = HealAmount * HotBonusMultiplier;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.Heal(ctx.FinalValue);
	}
}
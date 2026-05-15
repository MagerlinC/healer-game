using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Sanguimancy;

/// <summary>
/// Transfuses the caster's own life force directly into an ally.
///
/// Costs health instead of mana — useful when mana runs dry but comes at the
/// risk of the caster's own survival.  Converts at a 1:2+ ratio, making it the
/// most health-efficient heal in the Sanguimancy school.
///
/// The HP cost is taken via <see cref="Character.TakeDamage"/> and is therefore
/// subject to the caster's own shields and damage-reduction buffs.
/// </summary>
[GlobalClass]
public partial class VitalSurgeSpell : SpellResource
{
	const float HealAmount = 35f;
	const float HpCostAmount = 15f;


	public VitalSurgeSpell()
	{
		Name = "Vital Surge";
		Description =
			$"Sacrifice {HpCostAmount} health to transfuse your life force into an ally, restoring {HealAmount} health. Cannot target the caster.";
		ManaCost = 0f;
		HealthCost = HpCostAmount;
		CastTime = 1.5f;
		Cooldown = 2f;
		School = SpellSchool.Sanguimancy;
		Tags = SpellTags.Healing;
		TargetingType = TargetingType.Ally;
		RequiredSchoolPoints = 0;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/vital-surge.png");
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		// Don't allow targeting self
		return explicitTarget == caster ? [] : base.ResolveTargets(caster, explicitTarget);
	}

	public override float GetBaseValue()
	{
		return HealAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		ctx.Target?.Heal(ctx.FinalValue);
	}
}
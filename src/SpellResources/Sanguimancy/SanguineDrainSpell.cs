using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Sanguimancy;

/// <summary>
/// Tears vitality from the enemy and disperses it across the whole party.
///
/// The caster bleeds a small amount to open the siphon channel, deals significant
/// damage to the boss, and the stolen essence washes over all living allies as
/// a burst of healing.  This is the Sanguimancy school's primary sustain tool —
/// offence and group recovery in a single bloody rite.
///
/// ResolveTargets returns the boss so the pipeline logs and amplifies the damage
/// correctly.  The group heal is applied directly inside Apply.
/// </summary>
[GlobalClass]
public partial class SanguineDrainSpell : SpellResource
{
	const float DamageAmount = 28f;
	const float HealPerAlly = 10f;
	const float HpCostAmount = 10f;

	public SanguineDrainSpell()
	{
		Name = "Sanguine Drain";
		Description =
			$"Spend {HpCostAmount} health to siphon vitality from the enemy, dealing {DamageAmount} damage and healing all allies for {HealPerAlly} health.";
		ManaCost = 0f;
		HealthCost = HpCostAmount;
		CastTime = 1.5f;
		Cooldown = 8f;
		School = SpellSchool.Sanguimancy;
		Tags = SpellTags.Damage | SpellTags.Healing | SpellTags.GroupSpell;
		RequiredSchoolPoints = 1;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/sanguine-drain.png");
	}

	/// <summary>
	/// Always resolves to the boss so the spell pipeline treats this as a
	/// damage spell against the correct target.
	/// </summary>
	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		foreach (var node in caster.GetTree().GetNodesInGroup(GameConstants.BossGroupName))
			if (node is Character { IsAlive: true } boss)
				return [boss];
		return [explicitTarget];
	}

	public override float GetBaseValue()
	{
		return DamageAmount;
	}

	public override void Apply(SpellContext ctx)
	{
		// Deal damage to the boss (FinalValue has been run through the modifier pipeline).
		ctx.Target?.TakeDamage(ctx.FinalValue);

		// Disperse the stolen vitality across all living allies.
		foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is Character { IsAlive: true } ally)
				ally.Heal(HealPerAlly);
		}
	}
}
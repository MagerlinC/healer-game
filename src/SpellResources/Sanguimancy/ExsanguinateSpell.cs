using System;
using System.Collections.Generic;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources.Sanguimancy;

/// <summary>
/// The ultimate Sanguimancy nuke — tears the blood from every living party
/// member (including the caster) and hurls the combined life force at the boss.
///
/// Damage scales with how many allies are alive and how much they can afford to
/// lose.  The drain is never lethal: each member loses at most enough to leave
/// them at 1 HP.  This creates a tense risk/reward: more healthy allies means
/// a bigger hit, but the caster must heal the party back up afterwards.
///
/// Synergises tightly with Vital Surge and Sanguine Drain, which can recover
/// the party between pulls.
/// </summary>
[GlobalClass]
public partial class ExsanguinateSpell : SpellResource
{
	/// <summary>Health drained from each living party member per cast.</summary>
	const float DrainPerMember = 18f;

	const float DamageMultiplier = 2f;

	// HpCost reports per-member drain so talents (Crimson Rebound, Sanguine Ward)
	// can react to it in a meaningful way.

	public ExsanguinateSpell()
	{
		Name = "Exsanguinate";
		Description =
			$"Extract {DrainPerMember} health from each living party member (non-lethal), dealing twice the total as damage to the boss.";
		ManaCost = 0f;
		CastTime = 2.5f;
		HealthCost = 5f;
		Cooldown = 12f;
		School = SpellSchool.Sanguimancy;
		Tags = SpellTags.Damage;
		RequiredSchoolPoints = 2;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "sanguimancy/exsanguinate.png");
	}

	/// <summary>
	/// Always resolves to the boss — the party drain is handled in Apply.
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
		return 0f;
	}

	/// <summary>
	/// Pre-compute how much health the party can contribute (non-lethal) and set
	/// <see cref="SpellContext.BaseValue"/> to the resulting damage so the full
	/// value flows through multipliers, crit, and the central combat-log correctly.
	/// Previously GetBaseValue returned 0, causing the pipeline to log 0 damage
	/// and Apply to log its own value with the wrong CombatEventType.
	/// </summary>
	public override void OnAfterTargetsResolved(SpellContext ctx)
	{
		var totalDrained = 0f;
		foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character { IsAlive: true } member) continue;
			var drain = Math.Min(DrainPerMember, member.CurrentHealth - 1f);
			if (drain > 0f)
				totalDrained += drain;
		}
		ctx.BaseValue = totalDrained * DamageMultiplier;
	}

	public override void Apply(SpellContext ctx)
	{
		// Drain each party member. ctx.FinalValue (set via OnAfterTargetsResolved)
		// already carries the correct damage amount including modifiers and crit;
		// the pipeline handles logging and floating combat text centrally.
		foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character { IsAlive: true } member) continue;
			var drain = Math.Min(DrainPerMember, member.CurrentHealth - 1f);
			if (drain > 0f)
				member.SpendLife(drain);
		}

		if (ctx.FinalValue > 0f)
			ctx.Target?.TakeDamage(ctx.FinalValue);
	}
}
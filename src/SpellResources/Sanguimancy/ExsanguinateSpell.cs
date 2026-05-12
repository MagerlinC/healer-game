using System;
using System.Collections.Generic;
using Godot;
using healerfantasy.CombatLog;
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

	// GetBaseValue returns 0: the actual damage is computed dynamically in Apply
	// based on party composition, bypassing the standard pipeline value.
	public override float GetBaseValue()
	{
		return 0f;
	}

	public override void Apply(SpellContext ctx)
	{
		var totalDrained = 0f;

		// Collect living party members.
		foreach (var node in ctx.Caster.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character { IsAlive: true } member) continue;

			// Drain is non-lethal: leave at least 1 HP.
			var drain = Math.Min(DrainPerMember, member.CurrentHealth - 1f);
			if (drain <= 0f) continue;

			member.SpendLife(drain);
			totalDrained += drain;
		}

		// All extracted life becomes a concentrated blow on the boss.
		if (totalDrained > 0f)
		{
			var damageDealt = totalDrained * DamageMultiplier;

			ctx.Target?.TakeDamage(damageDealt);
			ctx.Target?.RaiseFloatingCombatText(damageDealt, false, (int)School, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = ctx.Caster.CharacterName,
				TargetName = ctx.Target?.CharacterName,
				AbilityName = "Exsanguinate",
				Amount = damageDealt,
				Description = Description,
				Type = CombatEventType.Healing,
				IsCrit = false
			});
		}
	}
}
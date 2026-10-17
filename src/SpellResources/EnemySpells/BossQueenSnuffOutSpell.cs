using System.Collections.Generic;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// The Tallow Queen — Snuff Out.
///
/// This spell is never cast through the normal SpellPipeline — it is resolved
/// directly by <see cref="TheTallowQueen.EndWaxShell"/> when the
/// 8-second Wax Shell cast timer completes with the shield still intact.
///
/// Call <see cref="ResolveNow"/> from the boss to deal the damage to every
/// living party member. The cast bar countdown and cancellation logic are
/// handled entirely in <see cref="TheTallowQueen"/>.
/// </summary>
[GlobalClass]
public partial class BossQueenSnuffOutSpell : SpellResource
{
	/// <summary>Damage dealt to every living party member when Snuff Out completes.</summary>
	public float DamageAmount = 1000f;

	/// <summary>How long the Snuff Out cast bar runs (matches Wax Shell shield window).</summary>
	public float CastDuration = 8f;

	public BossQueenSnuffOutSpell()
	{
		Name = "Snuff Out";
		Description = $"The Queen pinches out every flame - deals {1000:F0} damage to every living party member. " +
		              $"Break the Queen's Wax Shell before the cast completes to prevent it.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		Icon = GD.Load<Texture2D>(AssetConstants.SpellIconAssets + "enemy/the-tallow-queen/snuff-out.png");
		CastTime = 0f; // resolved manually, not through the pipeline cast time
		TargetingType = TargetingType.Enemy;
	}

	/// <summary>
	/// Deals <see cref="DamageAmount"/> to every living party member.
	/// Called by <see cref="TheTallowQueen"/> when the Wax Shell cast
	/// completes without the shield being broken.
	/// </summary>
	public void ResolveNow(Character boss)
	{
		if (boss == null || !IsInstanceValid(boss)) return;

		foreach (var node in boss.GetTree().GetNodesInGroup("party"))
		{
			if (node is not Character target || !target.IsAlive) continue;

			target.TakeDamage(DamageAmount);
			target.RaiseFloatingCombatText(DamageAmount, false, (int)SpellSchool.Generic, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = boss.CharacterName,
				TargetName = target.CharacterName,
				AbilityName = Name,
				Amount = DamageAmount,
				Type = CombatEventType.Damage,
				IsCrit = false,
				Description = "The Queen's Snuff Out resolves - every flame in the spire is pinched out at once."
			});
		}

		GD.Print($"[SnuffOut] Resolved — {DamageAmount:F0} damage to entire party.");
	}

	/// <summary>Not used — this spell bypasses the normal pipeline Apply.</summary>
	public override void Apply(SpellContext ctx)
	{
	}

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
	{
		return new List<Character>();
	}
}
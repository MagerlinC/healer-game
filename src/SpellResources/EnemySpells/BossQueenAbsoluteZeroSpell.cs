using System.Collections.Generic;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;

namespace healerfantasy.SpellResources;

/// <summary>
/// Queen of the Frozen Wastes — Absolute Zero.
///
/// This spell is never cast through the normal SpellPipeline — it is resolved
/// directly by <see cref="QueenOfTheFrozenWastes.EndIceBlock"/> when the
/// 8-second Ice Block cast timer completes with the shield still intact.
///
/// Call <see cref="ResolveNow"/> from the boss to deal the damage to every
/// living party member. The cast bar countdown and cancellation logic are
/// handled entirely in <see cref="QueenOfTheFrozenWastes"/>.
/// </summary>
[GlobalClass]
public partial class BossQueenAbsoluteZeroSpell : SpellResource
{
	/// <summary>Damage dealt to every living party member when Absolute Zero completes.</summary>
	public float DamageAmount = 1000f;

	/// <summary>How long the Absolute Zero cast bar runs (matches Ice Block shield window).</summary>
	public float CastDuration = 8f;

	public BossQueenAbsoluteZeroSpell()
	{
		Name = "Absolute Zero";
		Description = $"The final cold — deals {1000:F0} damage to every living party member. " +
		              $"Break the Queen's Ice Block shield before the cast completes to prevent it.";
		Tags = SpellTags.Damage;
		ManaCost = 0f;
		CastTime = 0f; // resolved manually, not through the pipeline cast time
		EffectType = EffectType.Harmful;
	}

	/// <summary>
	/// Deals <see cref="DamageAmount"/> to every living party member.
	/// Called by <see cref="QueenOfTheFrozenWastes"/> when the Ice Block cast
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
				Timestamp   = Time.GetTicksMsec() / 1000.0,
				SourceName  = boss.CharacterName,
				TargetName  = target.CharacterName,
				AbilityName = Name,
				Amount      = DamageAmount,
				Type        = CombatEventType.Damage,
				IsCrit      = false,
				Description = "The Queen's Absolute Zero resolves — a killing frost that obliterates all warmth."
			});
		}

		GD.Print($"[AbsoluteZero] Resolved — {DamageAmount:F0} damage to entire party.");
	}

	/// <summary>Not used — this spell bypasses the normal pipeline Apply.</summary>
	public override void Apply(SpellContext ctx) { }

	public override List<Character> ResolveTargets(Character caster, Character explicitTarget)
		=> new();
}

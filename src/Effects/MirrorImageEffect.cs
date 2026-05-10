using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// Applied by Mirror Image (Chronomancy ultimate).
///
/// While active, every spell the caster casts is echoed by their mirror twin at
/// 50% power. The copy targets a random valid recipient (ally for healing, enemy
/// for damage) and applies the flat value directly — it does NOT re-run the full
/// spell Apply, so there are no secondary effects such as HoT/DoT applications.
/// This cleanly sidesteps effect-ID collision between the original and the echo.
/// </summary>
public partial class MirrorImageEffect : CharacterEffect, ISpellModifier
{
	const float MirrorFraction = 0.5f;

	public MirrorImageEffect(float duration) : base(duration)
	{
		EffectId = "MirrorImageEffect";
	}

	// ── ISpellModifier ────────────────────────────────────────────────────────

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	/// <summary>
	/// After every cast: fire the echo at 50% of the resolved FinalValue.
	/// Skips ultimates, utility spells, and any cast with no meaningful value.
	/// </summary>
	public void OnAfterCast(SpellContext ctx)
	{
		// Don't echo ultimates or the mirror buff itself being re-applied.
		if (ctx.Spell is SpellResources.Void.UltimateSpellResource) return;
		if (ctx.Spell.School == SpellSchool.Generic) return;

		var mirrorValue = ctx.FinalValue * MirrorFraction;
		if (mirrorValue <= 0f) return;

		var isDamage = ctx.Tags.HasFlag(SpellTags.Damage);
		var isHealing = ctx.Tags.HasFlag(SpellTags.Healing) && !isDamage;

		if (isDamage)
		{
			var enemies = ctx.Caster.CollectAliveEnemies();
			if (enemies.Count == 0) return;
			var target = enemies[(int)(GD.Randi() % (uint)enemies.Count)];

			target.TakeDamage(mirrorValue);
			target.RaiseFloatingCombatText(mirrorValue, false, (int)School, false);
			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = target.CharacterName,
				AbilityName = "Mirror Image",
				Amount = mirrorValue,
				Description = Description,
				Type = CombatEventType.Damage,
				IsCrit = false
			});
		}
		else if (isHealing)
		{
			var allies = ctx.Caster.CollectAlivePartyMembers();
			if (allies.Count == 0) return;
			var target = allies[(int)(GD.Randi() % (uint)allies.Count)];

			target.Heal(mirrorValue);
			target.RaiseFloatingCombatText(mirrorValue, true, (int)School, false);
			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = target.CharacterName,
				AbilityName = "Mirror Image",
				Amount = mirrorValue,
				Description = Description,
				Type = CombatEventType.Healing,
				IsCrit = false
			});
		}
	}
}
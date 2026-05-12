using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// Applied by Vampiric Embrace (Sanguimancy ultimate).
///
/// While active (10 s by default):
///   • Every second, all living party members lose <see cref="_drainPerSecond"/> HP.
///   • Every time the caster deals direct damage, <see cref="_leechRate"/> × that
///     damage is redistributed as healing to all living party members. The total
///     healed is tracked in <see cref="_totalLeeched"/>.
///
/// On expiry (natural or removed):
///   • All living enemies are struck for <see cref="_damageMultiplierOnExpire"/> ×
///     <see cref="_totalLeeched"/>, representing the accumulated sanguine energy
///     released in one explosive burst.
/// </summary>
public partial class VampiricEmbraceEffect : CharacterEffect, ISpellModifier
{
	readonly float _drainPerSecond;
	readonly float _leechRate;
	readonly float _damageMultiplierOnExpire;
	float _totalLeeched = 0f;

	public VampiricEmbraceEffect(
		float drainPerSecond,
		float leechRate,
		float damageMultiplierOnExpire,
		float duration
	) : base(duration, 1f) // 1 s tick interval for the party drain
	{
		EffectId = "VampiricEmbraceEffect";
		_drainPerSecond = drainPerSecond;
		_leechRate = leechRate;
		_damageMultiplierOnExpire = damageMultiplierOnExpire;
		IsUltimateEffect = true;
	}

	// ── Per-second tick: drain all party members ─────────────────────────────

	protected override void OnTick(Character target)
	{
		foreach (var member in target.CollectAlivePartyMembers())
		{
			member.TakeDamage(_drainPerSecond);
			member.RaiseFloatingCombatText(_drainPerSecond, false, (int)School, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = member.CharacterName,
				AbilityName = AbilityName ?? EffectId,
				Amount = _drainPerSecond,
				Description = Description,
				Type = CombatEventType.Damage,
				IsCrit = false
			});
		}
	}

	// ── On expiry: release the accumulated sanguine burst ────────────────────

	public override void OnExpired(Character target)
	{
		if (_totalLeeched <= 0f) return;

		var burstDamage = _totalLeeched * _damageMultiplierOnExpire;
		foreach (var enemy in target.CollectAliveEnemies())
		{
			enemy.TakeDamage(burstDamage);
			enemy.RaiseFloatingCombatText(burstDamage, false, (int)School, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = enemy.CharacterName,
				AbilityName = "Vampiric Embrace (Burst)",
				Amount = burstDamage,
				Description = Description,
				Type = CombatEventType.Damage,
				IsCrit = false
			});
		}
	}

	// ── ISpellModifier: intercept damage casts to apply the leech ────────────

	public ModifierPriority Priority => ModifierPriority.BASE;

	public void OnBeforeCast(SpellContext ctx)
	{
	}

	public void OnCalculate(SpellContext ctx)
	{
	}

	/// <summary>
	/// After any damage spell the caster lands: heal the whole party for
	/// <see cref="_leechRate"/> × FinalValue, and track that amount toward the
	/// end-of-buff burst.
	/// </summary>
	public void OnAfterCast(SpellContext ctx)
	{
		if (!ctx.Tags.HasFlag(SpellTags.Damage)) return;
		if (ctx.FinalValue <= 0f) return;

		var leechAmount = ctx.FinalValue * _leechRate;
		_totalLeeched += leechAmount;

		foreach (var member in ctx.Caster.CollectAlivePartyMembers())
		{
			member.Heal(leechAmount);
			member.RaiseFloatingCombatText(leechAmount, true, (int)School, false);

			CombatLog.CombatLog.Record(new CombatEventRecord
			{
				Timestamp = Time.GetTicksMsec() / 1000.0,
				SourceName = SourceCharacterName,
				TargetName = member.CharacterName,
				AbilityName = "Vampiric Embrace (Leech)",
				Amount = leechAmount,
				Description = Description,
				Type = CombatEventType.Healing,
				IsCrit = false
			});
		}
	}
}
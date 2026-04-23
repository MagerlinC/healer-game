using Godot;
using healerfantasy.CombatLog;

namespace healerfantasy.Effects;

/// <summary>
/// Heals the target for <see cref="DamagePerTick"/> every
/// <see cref="TickInterval"/> seconds for <see cref="CharacterEffect.Duration"/> seconds.
/// </summary>
public partial class DamageOverTimeEffect : CharacterEffect
{
	public float DamagePerTick { get; }
	public float TickInterval { get; }

	public DamageOverTimeEffect(float damagePerTick, float duration, float tickInterval = 1f, bool isDispellable = true)
		: base(duration, tickInterval)
	{
		DamagePerTick = damagePerTick;
		TickInterval = tickInterval;
		SourceCharacterName = SourceCharacterName;
		IsDispellable = isDispellable;
	}

	protected override void OnTick(Character target)
	{
		target.TakeDamage(DamagePerTick);
		target.RaiseFloatingCombatText(DamagePerTick, false, (int)School, false);

		if (SourceCharacterName == null) return;

		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = SourceCharacterName,
			TargetName = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount = DamagePerTick,
			Type = CombatEventType.Damage,
			IsCrit = false
		});
	}
}
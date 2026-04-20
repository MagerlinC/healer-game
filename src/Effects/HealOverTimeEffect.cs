using Godot;
using healerfantasy.CombatLog;

namespace healerfantasy.Effects;

/// <summary>
/// Heals the target for <see cref="HealPerTick"/> every
/// <see cref="TickInterval"/> seconds for <see cref="CharacterEffect.Duration"/> seconds.
/// </summary>
public partial class HealOverTimeEffect : CharacterEffect
{
	public float HealPerTick { get; }
	public float TickInterval { get; }

	/// <param name="healPerTick">Health restored on each tick.</param>
	/// <param name="duration">Total effect duration in seconds.</param>
	/// <param name="tickInterval">Seconds between heals. Defaults to 1.</param>
	public HealOverTimeEffect(float healPerTick, float duration, float tickInterval = 1f)
		: base(duration, tickInterval)
	{
		HealPerTick = healPerTick;
		TickInterval = tickInterval;
	}

	protected override void OnTick(Character target)
	{
		target.Heal(HealPerTick);

		if (SourceCharacterName == null) return;

		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = SourceCharacterName,
			TargetName = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount = HealPerTick,
			Type = CombatEventType.Healing,
			IsCrit = false
		});
	}
}
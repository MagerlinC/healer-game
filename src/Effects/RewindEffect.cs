using Godot;
using healerfantasy.CombatLog;

namespace healerfantasy.Effects;

public partial class RewindEffect : CharacterEffect
{
	float _healthWhenCast;

	/// <param name="duration">Total effect duration in seconds.</param>
	/// <param name="tickInterval">Seconds between heals. Defaults to 1.</param>
	public RewindEffect(float duration, float tickInterval = 1f)
		: base(duration, tickInterval)
	{
	}

	public override void OnApplied(Character target)
	{
		_healthWhenCast = target.CurrentHealth;
	}

	public override void OnExpired(Character target)
	{
		var missingHealth = _healthWhenCast - target.CurrentHealth;
		if (!(missingHealth > 0)) return;

		target.Heal(missingHealth);
		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = SourceCharacterName,
			TargetName = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount = missingHealth,
			Type = CombatEventType.Healing,
			IsCrit = false
		});
	}
}
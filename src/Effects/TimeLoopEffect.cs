using Godot;
using healerfantasy.CombatLog;
using healerfantasy.SpellSystem;
using healerfantasy.Talents;

namespace healerfantasy.Effects;

/// <summary>
/// A time-delayed heal. When the effect expires naturally, the target is healed
/// for <see cref="DamageFraction"/>. This creates a "safety net" mechanic: cast it
/// on an ally and, after the delay, they receive a burst of healing.
///
/// If the target dies before the effect expires, <see cref="OnExpired"/> still
/// runs but <see cref="Character.Heal"/> is a no-op on dead characters.
/// </summary>
public partial class TimeLoopEffect : CharacterEffect
{
	public float DamageFraction { get; }

	float _originalTargetHealth = 0f;

	public TimeLoopEffect(float duration, float damageFraction)
		: base(duration, 0f)
	{
		EffectId = "TimeLoop";
		DamageFraction = damageFraction;
	}

	public override void OnExpired(Character target)
	{
		var damageTaken = target.CurrentHealth - _originalTargetHealth;
		target.TakeDamage(damageTaken * DamageFraction);
		target.RaiseFloatingCombatText(damageTaken, false, (int)School, false);

		if (SourceCharacterName == null) return;
		CombatLog.CombatLog.Record(new CombatEventRecord
		{
			Timestamp = Time.GetTicksMsec() / 1000.0,
			SourceName = SourceCharacterName,
			TargetName = target.CharacterName,
			AbilityName = AbilityName ?? EffectId,
			Amount = damageTaken,
			Description = Description,
			Type = CombatEventType.Damage,
			IsCrit = false
		});
	}
}
namespace healerfantasy.Effects;

public partial class BoonOfTheWildsEffect : CharacterEffect
{
	public float HealthIncreaseFraction { get; }

	// Stored so OnExpired removes exactly what OnApplied added, regardless of
	// any MaxHealth changes that happen while the buff is active.
	float _appliedBonus;

	public BoonOfTheWildsEffect(float duration, float healthIncreaseFraction)
		: base(duration, 0f)
	{
		EffectId = "BoonOfTheWilds";
		HealthIncreaseFraction = healthIncreaseFraction;
	}

	public override void OnApplied(Character target)
	{
		_appliedBonus = target.MaxHealth * HealthIncreaseFraction;
		target.ModifyMaxHealth(_appliedBonus);
	}

	public override void OnExpired(Character target)
	{
		target.ModifyMaxHealth(-_appliedBonus);
	}
}
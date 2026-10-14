namespace healerfantasy.Effects;

public partial class DripEffect : CharacterEffect
{
	public float ManaAmount { get; }

	/// <param name="duration">Seconds before the heal fires.</param>
	/// <param name="manaAmount">Health restored on expiry.</param>
	public DripEffect(float duration, float manaAmount)
		: base(duration, 0f)
	{
		EffectId = "Drip";
		ManaAmount = manaAmount;
	}

	/// <summary>Fires the delayed heal when the duration runs out.</summary>
	public override void OnExpired(Character target)
	{
		target.RestoreMana(ManaAmount);
	}
}
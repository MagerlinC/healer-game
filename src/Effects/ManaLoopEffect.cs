namespace healerfantasy.Effects;

public partial class ManaLoopEffect : CharacterEffect
{
	public float ManaAmount { get; }

	/// <param name="duration">Seconds before the heal fires.</param>
	/// <param name="manaAmount">Health restored on expiry.</param>
	public ManaLoopEffect(float duration, float manaAmount)
		: base(duration, 0f)
	{
		EffectId = "ManaLoop";
		ManaAmount = manaAmount;
	}

	/// <summary>Fires the delayed heal when the duration runs out.</summary>
	public override void OnExpired(Character target)
	{
		target.RestoreMana(ManaAmount);
	}
}
namespace healerfantasy;

/// <summary>
/// Applies a damage-absorbing shield to a character for a fixed duration.
///
/// On application the shield value is added to the character's
/// <see cref="Character.CurrentShield"/>. Incoming damage is absorbed by the
/// shield before it reaches health (see <see cref="Character.TakeDamage"/>).
///
/// On expiry the original shield amount is removed from the character's
/// remaining shield (clamped at 0), so any damage already absorbed is
/// correctly accounted for.
/// </summary>
public partial class ShieldEffect : CharacterEffect
{
	readonly float _shieldAmount;

	/// <param name="amount">Total shield points added.</param>
	/// <param name="duration">How long the shield lasts in seconds.</param>
	public ShieldEffect(float amount, float duration)
		: base(duration, 0f)
	{
		EffectId = "ShieldingReinvigoration";
		_shieldAmount = amount;
	}

	public override void OnApplied(Character target)
	{
		target.AddShield(_shieldAmount);
	}

	public override void OnExpired(Character target)
	{
		target.RemoveShield(_shieldAmount);
	}

	/// <summary>
	/// Expire early if the shield has been fully consumed by incoming damage.
	/// This causes the buff indicator to disappear immediately rather than
	/// lingering for the remaining duration with a depleted shield.
	/// </summary>
	protected override bool ShouldExpireEarly(Character target)
	{
		return target.CurrentShield <= 0f;
	}
}
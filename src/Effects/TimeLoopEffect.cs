namespace healerfantasy.Effects;

/// <summary>
/// A time-delayed heal. When the effect expires naturally, the target is healed
/// for <see cref="HealAmount"/>. This creates a "safety net" mechanic: cast it
/// on an ally and, after the delay, they receive a burst of healing.
///
/// If the target dies before the effect expires, <see cref="OnExpired"/> still
/// runs but <see cref="Character.Heal"/> is a no-op on dead characters.
/// </summary>
public partial class TimeLoopEffect : CharacterEffect
{
    /// <summary>Amount of health restored when the loop resolves.</summary>
    public float HealAmount { get; }

    /// <param name="duration">Seconds before the heal fires.</param>
    /// <param name="healAmount">Health restored on expiry.</param>
    public TimeLoopEffect(float duration, float healAmount)
        : base(duration, 0f)
    {
        EffectId = "TimeLoop";
        HealAmount = healAmount;
    }

    /// <summary>Fires the delayed heal when the duration runs out.</summary>
    public override void OnExpired(Character target)
    {
        target.Heal(HealAmount);
    }
}

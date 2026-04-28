using Godot;

namespace healerfantasy.Effects;

/// <summary>
/// Applied to the Blood Prince for the duration of the Sanguine Siphon channel.
///
/// On application it records the boss's current health and derives a "drain target":
///   drainTarget = recordedHealth − (10% of MaxHealth)
///
/// Every frame <see cref="ShouldExpireEarly"/> checks whether the boss has been
/// damaged down to that target. If so the debuff expires and fires
/// <see cref="OnHealthTargetReached"/>, causing the channel node to cancel the siphon
/// early (the intended player counter-play — burst the boss to break the links).
///
/// If the channel simply runs to its full duration the channel node removes the
/// debuff via <see cref="Character.RemoveEffect"/> before the callback is ever
/// invoked, so no spurious cancellation fires.
///
/// Lifetime is driven by <see cref="CharacterEffect.Duration"/>, which must match
/// the channel duration passed to <see cref="SanguineSiphonChannelNode"/>.
/// </summary>
public partial class SanguineDrainDebuff : CharacterEffect
{
    /// <summary>Boss health recorded at the moment the channel began.</summary>
    public float RecordedHealth { get; private set; }

    /// <summary>
    /// The health value the boss must reach (or fall below) to break the channel.
    /// Set in <see cref="OnApplied"/> once <see cref="RecordedHealth"/> is known.
    /// </summary>
    public float HealthTarget { get; private set; }

    /// <summary>
    /// Invoked when the boss's health drops to or below <see cref="HealthTarget"/>.
    /// The <see cref="SanguineSiphonChannelNode"/> wires this to cancel the channel.
    /// </summary>
    public System.Action OnHealthTargetReached { get; set; }

    bool _healthTargetReached;

    /// <param name="channelDuration">Must match the channel node's duration.</param>
    public SanguineDrainDebuff(float channelDuration) : base(channelDuration, 0f)
    {
        EffectId = "SanguineDrain";
        IsDispellable = false;
        IsHarmful = false; // Applied to the boss — shown as a debuff tracker, not a player debuff.
    }

    public override void OnApplied(Character target)
    {
        RecordedHealth = target.CurrentHealth;
        HealthTarget = Mathf.Max(0f, RecordedHealth - target.MaxHealth * 0.10f);

        GD.Print($"[SanguineDrain] Channel started. Boss HP: {RecordedHealth:F0} → " +
                 $"drain target: {HealthTarget:F0} ({target.MaxHealth * 0.10f:F0} required damage).");
    }

    protected override bool ShouldExpireEarly(Character target)
    {
        if (target.CurrentHealth > HealthTarget) return false;
        _healthTargetReached = true;
        return true;
    }

    public override void OnExpired(Character target)
    {
        // Only signal cancellation when the health target was actually reached.
        // When the channel completes naturally the channel node calls
        // Character.RemoveEffect("SanguineDrain"), which fires OnExpired with
        // _healthTargetReached = false, so the callback is not invoked.
        if (!_healthTargetReached) return;

        GD.Print($"[SanguineDrain] Health target reached — cancelling Sanguine Siphon channel.");
        var cb = OnHealthTargetReached;
        OnHealthTargetReached = null; // prevent re-entry if OnExpired is somehow called twice
        cb?.Invoke();
    }
}

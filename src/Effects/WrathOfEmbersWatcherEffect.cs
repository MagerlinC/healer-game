using System.Linq;

namespace healerfantasy.Effects;

/// <summary>
/// Invisible, infinite-duration watcher placed on every party member by the
/// Wrath of Embers staff. Polls the host character's active effects every
/// <see cref="TickIntervalSeconds"/> seconds; any plain
/// <see cref="HealOverTimeEffect"/> is silently replaced with a
/// <see cref="WrathOfEmbersHoTWrapper"/> so that when it expires a matching
/// DoT is applied to the boss.
///
/// One instance is placed on each living party member (including the player)
/// the first time any spell is cast while the staff is equipped.
/// </summary>
public partial class WrathOfEmbersWatcherEffect : CharacterEffect
{
	public const string WatcherEffectId = "WrathOfEmbersWatcher";

	const float TickIntervalSeconds = 0.1f;

	public WrathOfEmbersWatcherEffect() : base(GameConstants.InfiniteDuration, TickIntervalSeconds)
	{
		EffectId    = WatcherEffectId;
		IsInvisible = true;
	}

	/// <summary>Wrap any HoTs that were already active when the watcher was applied.</summary>
	public override void OnApplied(Character target) => WrapExistingHoTs(target);

	/// <summary>Poll every 0.1 s and wrap any newly-arrived unwrapped HoTs.</summary>
	protected override void OnTick(Character target) => WrapExistingHoTs(target);

	// ── helpers ───────────────────────────────────────────────────────────────

	static void WrapExistingHoTs(Character target)
	{
		foreach (var effect in target.GetAllEffects().ToList())
		{
			if (effect is not HealOverTimeEffect hot) continue;
			if (effect is WrathOfEmbersHoTWrapper) continue; // already wrapped

			// Silent removal skips OnExpired so the old effect's expiry logic
			// doesn't fire prematurely.
			target.RemoveEffectSilent(hot.EffectId);
			target.ApplyEffect(new WrathOfEmbersHoTWrapper(hot));
		}
	}
}

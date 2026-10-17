using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// Invisible, infinite-duration watcher applied to friendly characters by
/// <see cref="healerfantasy.Items.Amulets.TheHeartOfLight"/>.
///
/// Polls the host character's active effects every
/// <see cref="TickIntervalSeconds"/> seconds. When at least one non-harmful
/// Flame-school effect is present, it ensures <see cref="FlameProtectionActiveBuff"/>
/// is active (showing the icon and reducing damage). When no Flame effects
/// remain, it removes the buff.
///
/// The split means the party-frame indicator only lights up when protection
/// is genuinely in effect, not permanently from the moment the amulet is worn.
/// </summary>
public partial class FlameProtectionWatcherEffect : CharacterEffect
{
	public const string WatcherEffectId = "FlameProtection";

	const float TickIntervalSeconds = 0.25f;

	readonly float _damageReductionAmount;

	public FlameProtectionWatcherEffect(float damageReductionAmount)
		: base(GameConstants.InfiniteDuration, TickIntervalSeconds)
	{
		EffectId = WatcherEffectId;
		IsInvisible = true;
		_damageReductionAmount = damageReductionAmount;
	}

	/// <summary>Evaluate immediately in case Flame effects are already present.</summary>
	public override void OnApplied(Character target)
	{
		UpdateActiveBuff(target);
	}

	/// <summary>Re-evaluate every 0.25 s as Flame effects arrive and expire.</summary>
	protected override void OnTick(Character target)
	{
		UpdateActiveBuff(target);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	void UpdateActiveBuff(Character target)
	{
		var flameEffectActive = target.GetAllEffects().Any(IsTrackedFlameEffect);
		var buffPresent = target.GetEffectById(FlameProtectionActiveBuff.ActiveBuffEffectId) != null;

		if (flameEffectActive && !buffPresent)
			target.ApplyEffect(new FlameProtectionActiveBuff(_damageReductionAmount));
		else if (!flameEffectActive && buffPresent)
			target.RemoveEffect(FlameProtectionActiveBuff.ActiveBuffEffectId);
	}

	/// <summary>
	/// Returns true for non-harmful Flame-school effects that are not part of
	/// this system itself (watcher or active buff).
	/// </summary>
	static bool IsTrackedFlameEffect(CharacterEffect e)
	{
		return !e.IsHarmful
		       && e.School == SpellSchool.Flame
		       && e.EffectId != WatcherEffectId
		       && e.EffectId != FlameProtectionActiveBuff.ActiveBuffEffectId;
	}
}

/// <summary>
/// Visible indicator and damage-reduction modifier applied by
/// <see cref="FlameProtectionWatcherEffect"/> whenever at least one non-harmful
/// Flame effect is active on the character.
///
/// Implements <see cref="ICharacterModifier"/> unconditionally — the
/// condition is enforced by the watcher adding/removing this buff.
/// </summary>
public partial class FlameProtectionActiveBuff : CharacterEffect, ICharacterModifier
{
	public const string ActiveBuffEffectId = "FlameProtectionActive";

	readonly float _damageReductionAmount;

	public FlameProtectionActiveBuff(float damageReductionAmount)
		: base(GameConstants.InfiniteDuration, 0f)
	{
		EffectId = ActiveBuffEffectId;
		Icon = LastLight.Hud.LastLightIcons.ForItem(nameof(Items.Amulets.TheHeartOfLight))
		       ?? GD.Load<Texture2D>(AssetConstants.AmuletIconPath(2));
		Description = $"Damage taken reduced by {100 * damageReductionAmount:F0}% while a Flame effect is active.";
		_damageReductionAmount = damageReductionAmount;
	}

	public void Modify(CharacterStats stats)
	{
		stats.DamageTakenMultiplier *= 1f - _damageReductionAmount;
	}
}
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
/// Holy-school effect is present, it ensures <see cref="HolyProtectionActiveBuff"/>
/// is active (showing the icon and reducing damage). When no Holy effects
/// remain, it removes the buff.
///
/// The split means the party-frame indicator only lights up when protection
/// is genuinely in effect, not permanently from the moment the amulet is worn.
/// </summary>
public partial class HolyProtectionWatcherEffect : CharacterEffect
{
	public const string WatcherEffectId = "HolyProtection";

	const float TickIntervalSeconds = 0.25f;

	readonly float _damageReductionAmount;

	public HolyProtectionWatcherEffect(float damageReductionAmount)
		: base(GameConstants.InfiniteDuration, TickIntervalSeconds)
	{
		EffectId = WatcherEffectId;
		IsInvisible = true;
		_damageReductionAmount = damageReductionAmount;
	}

	/// <summary>Evaluate immediately in case Holy effects are already present.</summary>
	public override void OnApplied(Character target)
	{
		UpdateActiveBuff(target);
	}

	/// <summary>Re-evaluate every 0.25 s as Holy effects arrive and expire.</summary>
	protected override void OnTick(Character target)
	{
		UpdateActiveBuff(target);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	void UpdateActiveBuff(Character target)
	{
		var holyEffectActive = target.GetAllEffects().Any(IsTrackedHolyEffect);
		var buffPresent = target.GetEffectById(HolyProtectionActiveBuff.ActiveBuffEffectId) != null;

		if (holyEffectActive && !buffPresent)
			target.ApplyEffect(new HolyProtectionActiveBuff(_damageReductionAmount));
		else if (!holyEffectActive && buffPresent)
			target.RemoveEffect(HolyProtectionActiveBuff.ActiveBuffEffectId);
	}

	/// <summary>
	/// Returns true for non-harmful Holy-school effects that are not part of
	/// this system itself (watcher or active buff).
	/// </summary>
	static bool IsTrackedHolyEffect(CharacterEffect e)
	{
		return !e.IsHarmful
		       && e.School == SpellSchool.Holy
		       && e.EffectId != WatcherEffectId
		       && e.EffectId != HolyProtectionActiveBuff.ActiveBuffEffectId;
	}
}

/// <summary>
/// Visible indicator and damage-reduction modifier applied by
/// <see cref="HolyProtectionWatcherEffect"/> whenever at least one non-harmful
/// Holy effect is active on the character.
///
/// Implements <see cref="ICharacterModifier"/> unconditionally — the
/// condition is enforced by the watcher adding/removing this buff.
/// </summary>
public partial class HolyProtectionActiveBuff : CharacterEffect, ICharacterModifier
{
	public const string ActiveBuffEffectId = "HolyProtectionActive";

	readonly float _damageReductionAmount;

	public HolyProtectionActiveBuff(float damageReductionAmount)
		: base(GameConstants.InfiniteDuration, 0f)
	{
		EffectId = ActiveBuffEffectId;
		Icon = GD.Load<Texture2D>(AssetConstants.AmuletIconPath(2));
		Description = $"Damage taken reduced by {100 * damageReductionAmount:F0}% while a holy effect is active.";
		_damageReductionAmount = damageReductionAmount;
	}

	public void Modify(CharacterStats stats)
	{
		stats.DamageTakenMultiplier *= 1f - _damageReductionAmount;
	}
}
using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// A flat cast-speed buff applied to a character by the Time Warp spell.
///
/// Unlike <see cref="AccelerationEffect"/>, this does not stack — re-applying
/// simply refreshes the duration. Implements <see cref="ICharacterModifier"/>
/// so <see cref="Character.GetCharacterStats"/> picks up the bonus automatically.
/// </summary>
public partial class TimeWarpEffect : CharacterEffect, ICharacterModifier
{
	/// <summary>Flat cast-speed bonus while the effect is active.</summary>
	public float CastSpeedBonus { get; }

	public TimeWarpEffect(float duration, float castSpeedBonus = 0.20f)
		: base(duration, 0f)
	{
		EffectId = "TimeWarp";
		CastSpeedBonus = castSpeedBonus;
	}

	// ── ICharacterModifier ────────────────────────────────────────────────────

	/// <summary>
	/// Contributes a flat cast-speed bonus to the character's stat snapshot
	/// while this effect is active.
	/// </summary>
	public void Modify(CharacterStats stats)
	{
		stats.IncreasedCastSpeed += CastSpeedBonus;
	}
}
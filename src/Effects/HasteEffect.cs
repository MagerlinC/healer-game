using healerfantasy.SpellSystem;

namespace healerfantasy.Effects;

/// <summary>
/// A powerful cast-speed buff that dramatically accelerates the target's
/// spellcasting for its duration.
///
/// Unlike <see cref="AccelerationEffect"/> (which stacks from repeated casts),
/// Haste is a flat, non-stacking burst of speed from a dedicated spell.
/// Implements <see cref="ICharacterModifier"/> so the bonus integrates
/// automatically with <see cref="Character.GetCharacterStats"/>.
/// </summary>
public partial class HasteEffect : CharacterEffect, ICharacterModifier
{
	/// <summary>Flat cast-speed bonus while active. 0.40 = +40% haste.</summary>
	public float CastSpeedBonus { get; }

	public HasteEffect(float duration, float castSpeedBonus = 0.40f)
		: base(duration, 0f)
	{
		EffectId = "Haste";
		CastSpeedBonus = castSpeedBonus;
	}

	// ── ICharacterModifier ────────────────────────────────────────────────────

	public void Modify(CharacterStats stats)
	{
		stats.IncreasedHaste += CastSpeedBonus;
	}
}
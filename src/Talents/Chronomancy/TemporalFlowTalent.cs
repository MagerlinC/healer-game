using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Chronomancy;

/// <summary>
/// Passively bends time around the caster, granting a flat +15% cast speed
/// at all times. The Chronomancy school's baseline stat talent, ensuring any
/// Chronomancy build benefits from faster casting regardless of spell choices.
/// </summary>
public class TemporalFlowTalent : ICharacterModifier
{
	const float CastSpeedBonus = 0.15f;

	public void Modify(CharacterStats stats)
	{
		stats.IncreasedHaste += CastSpeedBonus;
	}
}
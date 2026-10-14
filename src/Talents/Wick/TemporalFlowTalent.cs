using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Wick;

/// <summary>
/// Passively bends time around the caster, granting a flat +15% haste
/// at all times. The Wick school's baseline stat talent, ensuring any
/// Wick build benefits from faster casting regardless of spell choices.
/// </summary>
public class TemporalFlowTalent : ICharacterModifier
{
	const float CastSpeedBonus = 0.15f;

	public void Modify(CharacterStats stats)
	{
		stats.IncreasedHaste += CastSpeedBonus;
	}
}
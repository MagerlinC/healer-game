using healerfantasy.SpellSystem;

namespace healerfantasy.Talents.Generic;

public class ManaRecoveryTalent : ICharacterModifier
{
	public void Modify(CharacterStats stats)
	{
		stats.ManaRegenPerSecond += 2f;
	}
}
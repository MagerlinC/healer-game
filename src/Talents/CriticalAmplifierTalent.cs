using healerfantasy.SpellSystem;

namespace healerfantasy.Talents;

/// <summary>
/// Passively adds +20% critical strike chance to the character's stats.
///
/// Implemented as an <see cref="ICharacterModifier"/> so it is folded into
/// the stat snapshot built at the start of every spell cast.
/// </summary>
public class CriticalAmplifierTalent : ICharacterModifier
{
    const float CritBonus = 0.20f; // +20 percentage points

    public void Modify(CharacterStats stats)
    {
        stats.CritChance += CritBonus;
    }
}

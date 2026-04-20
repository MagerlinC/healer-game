namespace healerfantasy.SpellSystem;

/// <summary>
/// A snapshot of a character's computed stats at the moment a spell is cast.
/// Created fresh each cast by aggregating the character's base values with all
/// active <see cref="ICharacterModifier"/>s from their talents.
/// </summary>
public class CharacterStats
{
    public float MaxHealth           { get; set; }
    public float MaxMana             { get; set; }

    /// <summary>0.0 – 1.0. E.g. 0.2 = 20% crit chance.</summary>
    public float CritChance          { get; set; }

    /// <summary>How much FinalValue is multiplied on a crit. Default 1.5×.</summary>
    public float CritMultiplier      { get; set; } = 1.5f;

    /// <summary>Multiplier applied to all damage spells before modifier pipeline runs. Default 1.0.</summary>
    public float DamageMultiplier    { get; set; } = 1.0f;

    /// <summary>Multiplier applied to all healing spells before modifier pipeline runs. Default 1.0.</summary>
    public float HealingMultiplier   { get; set; } = 1.0f;
}

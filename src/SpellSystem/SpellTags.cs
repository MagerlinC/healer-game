namespace healerfantasy.SpellSystem;

/// <summary>
/// Flags that describe what a spell does or what element it belongs to.
/// Spells can carry multiple tags simultaneously (e.g. Fire | Damage).
/// Modifiers query these tags to decide whether they apply.
/// </summary>
[System.Flags]
public enum SpellTags
{
    None        = 0,
    Damage      = 1 << 0,
    Healing     = 1 << 1,
    Fire        = 1 << 2,
    Cold        = 1 << 3,
    Lightning   = 1 << 4,
    Critical    = 1 << 5,   // Set by the pipeline on a successful crit roll
    HealOverTime = 1 << 6,
    GroupSpell  = 1 << 7,
}

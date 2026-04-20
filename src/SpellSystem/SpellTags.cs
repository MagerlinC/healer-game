namespace healerfantasy.SpellSystem;

/// <summary>
/// Flags that describe what a spell does or what element it belongs to.
/// Spells can carry multiple tags simultaneously (e.g. Fire | Damage).
/// Modifiers query these tags to decide whether they apply.
/// </summary>
[System.Flags]
public enum SpellTags
{
	None = 0,
	Damage = 1 << 0,
	Healing = 1 << 1,
	Light = 1 << 2,
	Fire = 1 << 3,
	Cold = 1 << 4,
	Lightning = 1 << 5,
	Critical = 1 << 6, // Set by the pipeline on a successful crit roll
	Duration = 1 << 7,
	GroupSpell = 1 << 8
}
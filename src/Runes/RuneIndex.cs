namespace healerfantasy.Runes;

/// <summary>
/// Identifies each of the four runes by a 1-based index.
///
/// The index also defines unlock order: acquiring rune N requires completing
/// a full run with all runes 1 through N-1 active.
/// </summary>
public enum RuneIndex
{
    /// <summary>
    /// Rune of the Void — damage taken applies a healing absorption equal to
    /// 10% of damage dealt, consuming incoming heals until the absorbed amount
    /// is fully exhausted.
    /// </summary>
    Void = 1,

    /// <summary>
    /// Rune of Nature — growing vines spawn periodically during boss fights,
    /// attaching to a party member and dealing damage until killed.
    /// </summary>
    Nature = 2,

    /// <summary>
    /// Rune of Time — all bosses gain +10% haste (their abilities fire
    /// 10% more frequently).
    /// </summary>
    Time = 3,

    /// <summary>
    /// Rune of Purity — enables the "purest" form of each boss, unlocking
    /// extra mechanics tagged with <c>[RuneOfPurity]</c>.
    /// </summary>
    Purity = 4,
}

namespace healerfantasy.Runes;

/// <summary>
/// Identifies each of the five runes by a 1-based index. Each one deepens the night
/// a little further (names and text: <see cref="RuneInfo"/>).
///
/// The index also defines unlock order: acquiring rune N requires completing
/// a full run with all runes 1 through N-1 active.
/// </summary>
public enum RuneIndex
{
    /// <summary>
    /// Rune of Twilight — enemy hits leave a Shroud (a light absorption equal to
    /// 10% of the damage dealt) that swallows restored light until burned through.
    /// </summary>
    Twilight = 1,

    /// <summary>
    /// Rune of Nightfall — Grasping Thorns spawn periodically during boss fights,
    /// coiling around a party member and dealing damage until killed.
    /// </summary>
    Nightfall = 2,

    /// <summary>
    /// Rune of Midnight — all bosses gain +10% haste (their abilities fire
    /// 10% more frequently).
    /// </summary>
    Midnight = 3,

    /// <summary>
    /// Rune of the Starless Night — enables the truest form of each boss, unlocking
    /// extra mechanics tagged with <c>[RuneOfTheStarlessNight]</c>.
    /// </summary>
    StarlessNight = 4,

    /// <summary>
    /// Rune of the Abyss — opens a fifth, final dungeon past the Guttering Spire:
    /// The Abyss, home of The Infinite Abyss.
    /// </summary>
    Abyss = 5,
}

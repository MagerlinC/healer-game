using Godot;

namespace healerfantasy.Runes;

/// <summary>
/// Player-facing names and descriptions of the runes. Every rune makes the run
/// harder, so they read as the night deepening: twilight, nightfall, midnight,
/// a night without stars, and finally the Abyss itself.
/// </summary>
public static class RuneInfo
{
	public static string Name(RuneIndex rune) => rune switch
	{
		RuneIndex.Twilight => "Rune of Twilight",
		RuneIndex.Nightfall => "Rune of Nightfall",
		RuneIndex.Midnight => "Rune of Midnight",
		RuneIndex.StarlessNight => "Rune of the Starless Night",
		RuneIndex.Abyss => "Rune of the Abyss",
		_ => "Unknown Rune"
	};

	public static string Description(RuneIndex rune) => rune switch
	{
		RuneIndex.Twilight =>
			"The light begins to fail. Enemy hits leave a Shroud equal to 10% of the damage dealt.\n" +
			"Light restored to a shrouded ally is swallowed until the shroud is burned through.",
		RuneIndex.Nightfall =>
			"Night falls and the dark takes hold. Grasping Thorns rise and coil around party members, " +
			"dealing damage until they are cut down.",
		RuneIndex.Midnight =>
			"The dead of night, when the dark grows restless. All bosses gain +10% haste, " +
			"so their attacks and abilities come 10% more often.",
		RuneIndex.StarlessNight =>
			"Not a single star remains; yours is the only light. Every boss fights in its " +
			"truest, darkest form, unlocking its full strength.",
		RuneIndex.Abyss =>
			"Beyond the night lies only the Abyss. A final path opens past the Guttering Spire, " +
			"where The Infinite Abyss waits for the last light.",
		_ => ""
	};

	/// <summary>The rune's colour: fading from the last of the dusk to almost nothing.</summary>
	public static Color Color(RuneIndex rune) => rune switch
	{
		RuneIndex.Twilight => new Color(0.93f, 0.6f, 0.36f),
		RuneIndex.Nightfall => new Color(0.6f, 0.47f, 0.82f),
		RuneIndex.Midnight => new Color(0.38f, 0.48f, 0.78f),
		RuneIndex.Abyss => new Color(0.55f, 0.36f, 0.62f),
		_ => new Color(0.42f, 0.42f, 0.5f)
	};
}

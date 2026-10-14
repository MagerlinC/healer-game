using System.Collections.Generic;
using Godot;

namespace healerfantasy.SpellResources;

/// <summary>
/// Player-facing names, colours and descriptions for the spell schools.
///
/// Every school is a different relationship with the healer's own flame:
///   Flame    — the light itself: bright heals and searing bursts.
///   Ember    — light that lingers: smoulders, cinders, moths.
///   Umbra    — borrowing the dark: powerful, but it dims your light.
///   Wick     — time measured by a burning candle.
///   Pyre — burning your own life as fuel.
/// </summary>
public static class SchoolInfo
{
	public static string Name(SpellSchool school) => school switch
	{
		SpellSchool.Flame => "Flame",
		SpellSchool.Ember => "Ember",
		SpellSchool.Umbra => "Umbra",
		SpellSchool.Wick => "Wick",
		SpellSchool.Pyre => "Pyre",
		_ => "General"
	};

	/// <summary>Display name from a stored enum name (run history keeps <c>School.ToString()</c>).</summary>
	public static string Name(string storedSchool) =>
		TryParse(storedSchool, out var school) ? Name(school) : storedSchool;

	/// <summary>
	/// Parses a stored school name, including the pre-rename names
	/// (Holy, Nature, Void, Chronomancy, Sanguimancy, Kindling) found in older saves.
	/// </summary>
	public static bool TryParse(string storedSchool, out SpellSchool school)
	{
		if (storedSchool != null && LegacySchoolNames.TryGetValue(storedSchool, out school)) return true;
		return System.Enum.TryParse(storedSchool, out school);
	}

	static readonly Dictionary<string, SpellSchool> LegacySchoolNames = new()
	{
		["Holy"] = SpellSchool.Flame,
		["Nature"] = SpellSchool.Ember,
		["Void"] = SpellSchool.Umbra,
		["Chronomancy"] = SpellSchool.Wick,
		["Sanguimancy"] = SpellSchool.Pyre,
		["Kindling"] = SpellSchool.Pyre
	};

	/// <summary>
	/// Short flavour line plus the school's base mechanic, shown under the
	/// school name in the Spellbook.
	/// </summary>
	public static (string Flavour, string Mechanic) Description(SpellSchool school) => school switch
	{
		SpellSchool.Flame => ("The light itself, burning at its brightest.",
			"Casting Flame brightens your light and burns away a stack of Gloom."),
		SpellSchool.Ember => ("Light that lingers long after the fire.",
			"Ember spells that restore light leave a pocket of smouldering embers beneath the ally."),
		SpellSchool.Umbra => ("Power borrowed from the dark, at a price.",
			"Casting Umbra adds Gloom to the party: +4% damage taken per stack (max 5, 10s). Flame burns it away."),
		SpellSchool.Wick => ("Time, measured by a burning candle.",
			"Wick spells bend time: haste, wax that resolves later, light that rewinds. Your flame gutters, then flares."),
		SpellSchool.Pyre => ("Your own light, fed to the fire.",
			"Pyre spells burn your own light instead of kindling, trading it for power."),
		_ => ("The basics of any Lightkeeper's craft.", "")
	};

	public static Color Color(SpellSchool school) => school switch
	{
		SpellSchool.Flame => new Color(1f, 0.87f, 0.58f),        // white-gold flame
		SpellSchool.Ember => new Color(0.95f, 0.52f, 0.22f),   // ember orange
		SpellSchool.Umbra => new Color(0.62f, 0.48f, 0.88f),     // black-violet
		SpellSchool.Wick => new Color(0.72f, 0.84f, 0.95f), // pale wax-blue
		SpellSchool.Pyre => new Color(0.86f, 0.24f, 0.17f), // burning red
		_ => new Color(0.7f, 0.65f, 0.6f)
	};

	/// <summary>
	/// Old spell names → new, so loadouts saved before the rename still resolve.
	/// </summary>
	public static readonly Dictionary<string, string> LegacySpellNames = new()
	{
		["Burst of Light"] = "Flare",
		["Touch of Light"] = "Candle Touch",
		["Divine Aegis"] = "Lantern Ward",
		["Reinvigorate"] = "Rekindle",
		["Holy Nova"] = "Flashfire",
		["Wave of Incandescence"] = "Wildfire",
		["Archangel of Light"] = "Become the Sun",
		["Renewing Bloom"] = "Smoulder",
		["Wild Growth"] = "Spreading Embers",
		["Poison Bolt"] = "Cinder",
		["Leaf shade"] = "Ash Veil",
		["Swarm of the Forest"] = "Mothswarm",
		["Nourish"] = "Stoke",
		["One With Nature"] = "One With the Ash",
		["Shadow Bolt"] = "Umbral Bolt",
		["Void Drain"] = "Siphon the Dark",
		["Decay"] = "Rot",
		["Consuming Void"] = "Swallowing Dark",
		["Void's Embrace"] = "Embrace the Dark",
		["Haste"] = "Quicken the Wick",
		["Temporal Ward"] = "Wax Seal",
		["Mana Loop"] = "Drip",
		["Time Loop"] = "Relight",
		["Rewind"] = "Unburn",
		["Time Warp"] = "Burn Bright",
		["Mirror Image"] = "Twin Flame",
		["Exsanguinate"] = "Feed the Pyre",
		["Sanguine Drain"] = "Draw the Heat",
		["Sanguine Pact"] = "Burning Pact",
		["Vampiric Embrace"] = "Shared Flame",
		["Vital Surge"] = "Lifeflame"
	};

	/// <summary>Maps a possibly-old spell name to its current name.</summary>
	public static string CurrentSpellName(string name) =>
		name != null && LegacySpellNames.TryGetValue(name, out var current) ? current : name;
}

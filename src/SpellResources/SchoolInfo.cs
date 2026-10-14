using System.Collections.Generic;
using Godot;

namespace healerfantasy.SpellResources;

/// <summary>
/// Player-facing names and colours for the spell schools.
///
/// Every school is a different relationship with the healer's own flame:
///   Flame    (Holy)        — the light itself: bright heals and searing bursts.
///   Ember    (Nature)      — light that lingers: smoulders, cinders, moths.
///   Umbra    (Void)        — borrowing the dark: powerful, but it dims your light.
///   Wick     (Chronomancy) — time measured by a burning candle.
///   Kindling (Sanguimancy) — burning your own life as fuel.
///
/// The <see cref="SpellSchool"/> enum keeps its original identifiers so saves,
/// talents and modifiers are unaffected; only what the player reads changes.
/// </summary>
public static class SchoolInfo
{
	public static string Name(SpellSchool school) => school switch
	{
		SpellSchool.Holy => "Flame",
		SpellSchool.Nature => "Ember",
		SpellSchool.Void => "Umbra",
		SpellSchool.Chronomancy => "Wick",
		SpellSchool.Sanguimancy => "Kindling",
		_ => "General"
	};

	/// <summary>Display name from a stored enum name (run history keeps <c>School.ToString()</c>).</summary>
	public static string Name(string storedSchool) =>
		System.Enum.TryParse<SpellSchool>(storedSchool, out var school) ? Name(school) : storedSchool;

	public static Color Color(SpellSchool school) => school switch
	{
		SpellSchool.Holy => new Color(1f, 0.87f, 0.58f),        // white-gold flame
		SpellSchool.Nature => new Color(0.95f, 0.52f, 0.22f),   // ember orange
		SpellSchool.Void => new Color(0.62f, 0.48f, 0.88f),     // black-violet
		SpellSchool.Chronomancy => new Color(0.72f, 0.84f, 0.95f), // pale wax-blue
		SpellSchool.Sanguimancy => new Color(0.86f, 0.24f, 0.17f), // burning red
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

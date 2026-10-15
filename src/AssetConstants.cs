using Godot;
using healerfantasy.SpellResources;

namespace healerfantasy;

public static class AssetConstants
{

	public static Color SpellSchoolColor(SpellSchool school)
	{
		return SchoolInfo.Color(school);
	}
	public static readonly string SpellIconAssets = "res://assets/spell-icons/";
	public static readonly string TalentIconAssets = "res://assets/talent-icons/";

	public static readonly string FinalBossPhase2MusicPath = "res://assets/music/boss-battle.ogg";

	public static readonly string MistTexturePath = "res://assets/textures/mist.png";







	// ── Interactible assets ───────────────────────────────────────────────────

	public static readonly string SpellTomeInteractiblePath = "res://assets/interactibles/spell-tome.png";
	public static readonly string RunScrollInteractiblePath = "res://assets/interactibles/run-history-scroll.png";
	public static readonly string MapInteractiblePath = "res://assets/interactibles/map.png";
	public static readonly string ArmoryInteractiblePath = "res://assets/interactibles/armory.png";
	public static readonly string RuneTableInteractiblePath = "res://assets/interactibles/rune-table.png";
	public static readonly string NewsBoardInteractiblePath = "res://assets/interactibles/news-board.png";
	public static readonly string ExclamationInteractiblePath = "res://assets/interactibles/exclamation.png";

	// ── Talent tome assets (school affinity selector cards in the spellbook) ─

	public static readonly string TalentTomeFlamePath = "res://assets/interactibles/talent-tome-holy.png";
	public static readonly string TalentTomeEmberPath = "res://assets/interactibles/talent-tome-nature.png";
	public static readonly string TalentTomeUmbraPath = "res://assets/interactibles/talent-tome-void.png";
	public static readonly string TalentTomeWickPath = "res://assets/interactibles/talent-tome-chronomancy.png";
	public static readonly string TalentTomePyrePath = "res://assets/interactibles/talent-tome-sanguimancy.png";

	/// <summary>Returns the res:// path for the talent tome icon of the given spell school.</summary>
	public static string TalentTomePath(SpellSchool school)
	{
		return school switch
		{
			SpellSchool.Flame => TalentTomeFlamePath,
			SpellSchool.Ember => TalentTomeEmberPath,
			SpellSchool.Umbra => TalentTomeUmbraPath,
			SpellSchool.Wick => TalentTomeWickPath,
			SpellSchool.Pyre => TalentTomePyrePath,
			_ => TalentTomeFlamePath
		};
	}

	// ── Spell-book interactible (texture reflects active school affinity) ─────

	/// <summary>
	/// Returns the res:// path for the world-space spell-book interactible sprite.
	/// When <paramref name="affinity"/> is <c>null</c> (no affinity set), returns
	/// the neutral <c>spell-book.png</c>; otherwise returns the school-coloured variant.
	/// </summary>
	public static string GetSpellBookPathForAffinity(SpellSchool? affinity)
	{
		var suffix = affinity == null ? "" : "-" + SchoolAssetSuffix(affinity.Value);
		return $"res://assets/interactibles/spell-book/spell-book{suffix}.png";
	}

	/// <summary>School suffix used by the per-school image files, which keep their original names.</summary>
	static string SchoolAssetSuffix(SpellSchool school) => school switch
	{
		SpellSchool.Flame => "holy",
		SpellSchool.Ember => "nature",
		SpellSchool.Umbra => "void",
		SpellSchool.Wick => "chronomancy",
		SpellSchool.Pyre => "sanguimancy",
		_ => "generic"
	};

	// ── Merchant assets ───────────────────────────────────────────────────────
	public static readonly string StoneOfRebirthIconPath = "res://assets/characters/merchant/stone-of-rebirth.png";

	// ── Rune assets ───────────────────────────────────────────────────────────

	public static readonly string RuneFramePath = "res://assets/frames/rune-frame.png";
	public static readonly string RuneFrameActivePath = "res://assets/frames/rune-frame-active.png";

	// SFX

	public static readonly string CastingSfx = "res://assets/sound-effects/spell-casting/casting.wav";
	public static readonly string CastFinishedSfx = "res://assets/sound-effects/spell-casting/cast-finished.wav";

	public static readonly string DeflectRiserSoundPath = "res://assets/sound-effects/riser.mp3";
	public static readonly string DeflectFailedSoundPath = "res://assets/sound-effects/deflect-failed.mp3";
	public static readonly string RumbleSoundPath = "res://assets/sound-effects/rumble.mp3";
	public static readonly string LandingImpactSoundPath = "res://assets/sound-effects/landing-impact.wav";
	public static readonly string VictorySoundPath = "res://assets/music/victory.mp3";

	public static readonly string ButtonClickPath = "res://assets/sound-effects/button-impact.wav";

	public static readonly string RuneSfxPath = "res://assets/sound-effects/rune.wav";
	public static readonly string SpellbookSfxPath = "res://assets/sound-effects/book.wav";
	public static readonly string TalentsSfxPath = "res://assets/sound-effects/choir-hit.wav";
	public static readonly string WalkSfxPath = "res://assets/sound-effects/movement/walk.wav";

	// ── Thorns enemy assets ────────────────────────────────────────────────────


	// ── Equipment UI assets ───────────────────────────────────────────────────
	public static readonly string EquipmentSlotFramePath = "res://assets/frames/equipment-slot.png";

	// ── Item assets ───────────────────────────────────────────────────────────
	public static readonly string ItemsPath = "res://assets/items/";

	/// <summary>Returns the res:// path for staff icon n (1–5).</summary>
	public static string StaveIconPath(int n)
	{
		return $"{ItemsPath}staves/{n}.png";
	}

	public static string RingIconPath(int n)
	{
		return $"{ItemsPath}rings/{n}.png";
	}

	public static string AmuletIconPath(int n)
	{
		return $"{ItemsPath}amulets/{n}.png";
	}

	public static readonly string GoldIconPath = $"{ItemsPath}gold.png";

	// ── Scene backgrounds ─────────────────────────────────────────────────────


	public static readonly string CinzelFontPath = "res://assets/fonts/cinzel/Cinzel-VariableFont_wght.ttf";
}
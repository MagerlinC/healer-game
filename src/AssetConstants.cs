using healerfantasy.SpellResources;

namespace healerfantasy;

public static class AssetConstants
{
	public static readonly string EnemyAssets = "res://assets/enemies/";
	public static readonly string SpellIconAssets = "res://assets/spell-icons/";
	public static readonly string TalentIconAssets = "res://assets/talent-icons/";

	public static readonly string FinalBossPhase2MusicPath = "res://assets/music/boss-battle.ogg";

	public static readonly string MainMenuPath = "res://assets/backgrounds/main-menu/background.png";

	static readonly string BackgroundBasePath = "res://assets/backgrounds/";

	/// <summary>Arena background image paths, one per boss encounter (0–2).</summary>
	public static readonly string[] SpaceArenaBackgroundPaths =
	{
		BackgroundBasePath + "space/1.png",
		BackgroundBasePath + "space/2.png",
		BackgroundBasePath + "space/3.png"
	};

	public static readonly string[] ForsakenCitadelArenaBackgroundPaths =
	{
		BackgroundBasePath + "forsaken-citadel/1.png",
		BackgroundBasePath + "forsaken-citadel/1.png",
		BackgroundBasePath + "forsaken-citadel/1.png"
	};


	public static readonly string[] AncientKeepArenaBackgroundPaths =
	{
		BackgroundBasePath + "ancient-keep/1.png",
		BackgroundBasePath + "ancient-keep/1.png",
		BackgroundBasePath + "ancient-keep/1.png"
	};

	public static readonly string[] CastleOfBloodArenaBackgroundPaths =
	{
		BackgroundBasePath + "castle-of-blood/1.png",
		BackgroundBasePath + "castle-of-blood/1.png",
		BackgroundBasePath + "castle-of-blood/1.png"
	};

	/// <summary>Single background for the Frozen Peak — the final boss arena.</summary>
	public static readonly string[] FrozenPeakArenaBackgroundPaths =
	{
		BackgroundBasePath + "the-frozen-peak/background.png"
	};


	// ── Interactible assets ───────────────────────────────────────────────────

	public static readonly string SpellTomeInteractiblePath = "res://assets/interactibles/spell-tome.png";
	public static readonly string TalentBoardInteractiblePath = "res://assets/interactibles/talent-board.png";
	public static readonly string RunScrollInteractiblePath = "res://assets/interactibles/run-history-scroll.png";
	public static readonly string MapInteractiblePath = "res://assets/interactibles/map.png";
	public static readonly string ArmoryInteractiblePath = "res://assets/interactibles/armory.png";
	public static readonly string RuneTableInteractiblePath = "res://assets/interactibles/rune-table.png";

	// ── Talent tome assets (school affinity selector) ────────────────────────

	public static readonly string TalentTomeHolyPath = "res://assets/interactibles/talent-tome-holy.png";
	public static readonly string TalentTomeNaturePath = "res://assets/interactibles/talent-tome-nature.png";
	public static readonly string TalentTomeVoidPath = "res://assets/interactibles/talent-tome-void.png";
	public static readonly string TalentTomeChronomancyPath = "res://assets/interactibles/talent-tome-chronomancy.png";

	/// <summary>Returns the res:// path for the tome icon of the given spell school.</summary>
	public static string TalentTomePath(SpellSchool school) => school switch
	{
		SpellSchool.Holy        => TalentTomeHolyPath,
		SpellSchool.Nature      => TalentTomeNaturePath,
		SpellSchool.Void        => TalentTomeVoidPath,
		SpellSchool.Chronomancy => TalentTomeChronomancyPath,
		_                       => TalentTomeHolyPath
	};

	// ── Rune assets ───────────────────────────────────────────────────────────

	static readonly string RuneBasePath = "res://assets/interactibles/";
	public static readonly string RuneVoidIconPath = RuneBasePath + "rune-void.png";
	public static readonly string RuneNatureIconPath = RuneBasePath + "rune-nature.png";
	public static readonly string RuneTimeIconPath = RuneBasePath + "rune-time.png";
	public static readonly string RuneHolyIconPath = RuneBasePath + "rune-holy.png";

	public static readonly string RuneFramePath = "res://assets/frames/rune-frame.png";
	public static readonly string RuneFrameActivePath = "res://assets/frames/rune-frame-active.png";

	// SFX

	public static readonly string CastingSfx = "res://assets/sound-effects/spell-casting/casting.wav";
	public static readonly string CastFinishedSfx = "res://assets/sound-effects/spell-casting/cast-finished.wav";

	public static readonly string DeflectRiserSoundPath = "res://assets/sound-effects/riser.mp3";
	public static readonly string DeflectFailedSoundPath = "res://assets/sound-effects/deflect-failed.mp3";
	public static readonly string VictorySoundPath = "res://assets/music/victory.mp3";


	public static readonly string ButtonClickPath = "res://assets/sound-effects/button-impact.wav";

	public static readonly string RuneSfxPath = "res://assets/sound-effects/rune.wav";
	public static readonly string SpellbookSfxPath = "res://assets/sound-effects/book.wav";
	public static readonly string TalentsSfxPath = "res://assets/sound-effects/choir-hit.wav";

	/// <summary>Returns the icon path for rune N (1-based).</summary>
	public static string RuneIconPath(int runeIndex)
	{
		return runeIndex switch
		{
			1 => RuneVoidIconPath,
			2 => RuneNatureIconPath,
			3 => RuneTimeIconPath,
			4 => RuneHolyIconPath,
			_ => RuneVoidIconPath
		};
	}

	// ── Vines enemy assets ────────────────────────────────────────────────────

	static readonly string VinesBasePath = "res://assets/enemies/vines/";
	public static readonly string VinesFrame1Path = VinesBasePath + "grow1.png";
	public static readonly string VinesFrame2Path = VinesBasePath + "grow2.png";
	public static readonly string VinesFrame3Path = VinesBasePath + "grow3.png";

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

	// ── Scene backgrounds ─────────────────────────────────────────────────────

	public static readonly string OverworldBackgroundPath = BackgroundBasePath + "overworld/background.png";
	public static readonly string MapBackgroundPath = BackgroundBasePath + "map/world-map.png";
	public static readonly string CampBackgroundPath = BackgroundBasePath + "camp/camp.png";
}
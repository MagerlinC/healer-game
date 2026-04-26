namespace healerfantasy;

public static class AssetConstants
{
	public static readonly string EnemyAssets = "res://assets/enemies/";
	public static readonly string SpellIconAssets = "res://assets/spell-icons/";
	public static readonly string TalentIconAssets = "res://assets/talent-icons/";

	public static readonly string FinalBossPhase2MusicPath = "res://assets/music/boss-battle.ogg";

	public static readonly string MainMenuPath = "res://assets/backgrounds/main-menu/background.png";

	public static readonly string CastingSfx = "res://assets/sound-effects/spell-casting/casting.wav";
	public static readonly string CastFinishedSfx = "res://assets/sound-effects/spell-casting/cast-finished.wav";

	public static readonly string DeflectRiserSoundPath = "res://assets/sound-effects/riser.mp3";
	public static readonly string DeflectFailedSoundPath = "res://assets/sound-effects/deflect-failed.mp3";
	public static readonly string VictorySoundPath = "res://assets/music/victory.mp3";

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


	// ── Interactible assets ───────────────────────────────────────────────────

	public static readonly string SpellTomeInteractiblePath = "res://assets/interactibles/spell-tome.png";
	public static readonly string TalentBoardInteractiblePath = "res://assets/interactibles/talent-board.png";
	public static readonly string RunScrollInteractiblePath = "res://assets/interactibles/run-history-scroll.png";
	public static readonly string MapInteractiblePath = "res://assets/interactibles/map.png";
	public static readonly string ArmoryInteractiblePath = "res://assets/interactibles/armory.png";

	// ── Item assets ───────────────────────────────────────────────────────────

	/// <summary>Returns the res:// path for staff icon n (1–5).</summary>
	public static string StaveIconPath(int n)
	{
		return $"res://assets/items/staves/{n}.png";
	}

	// ── Scene backgrounds ─────────────────────────────────────────────────────

	public static readonly string OverworldBackgroundPath = BackgroundBasePath + "overworld/background.png";
	public static readonly string MapBackgroundPath = BackgroundBasePath + "map/world-map.png";
	public static readonly string CampBackgroundPath = BackgroundBasePath + "camp/camp.png";
}
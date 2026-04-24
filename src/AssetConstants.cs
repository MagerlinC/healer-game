namespace healerfantasy;

public static class AssetConstants
{
	public static readonly string EnemyAssets = "res://assets/enemies/";
	public static readonly string SpellIconAssets = "res://assets/spell-icons/";
	public static readonly string TalentIconAssets = "res://assets/talent-icons/";

	public static readonly string BossMusicPath = "res://assets/music/battle.wav";

	public static readonly string MainMenuPath = "res://assets/backgrounds/main-menu/background.png";

	public static readonly string CastingSfx = "res://assets/sound-effects/spell-casting/casting.wav";
	public static readonly string CastFinishedSfx = "res://assets/sound-effects/spell-casting/cast-finished.wav";

	public static readonly string ParryRiserSoundPath = "res://assets/sound-effects/riser.mp3";
	public static readonly string VictorySoundPath = "res://assets/music/victory.mp3";

	/// <summary>Arena background image paths, one per boss encounter (0–2).</summary>
	public static readonly string[] ArenaBackgroundPaths =
	{
		"res://assets/backgrounds/space/1.png",
		"res://assets/backgrounds/space/2.png",
		"res://assets/backgrounds/space/3.png"
	};

	// ── Interactible assets ───────────────────────────────────────────────────

	public static readonly string SpellTomeInteractiblePath = "res://assets/interactibles/spell-tome.png";
	public static readonly string TalentBoardInteractiblePath = "res://assets/interactibles/talent-board.png";
	public static readonly string RunScrollInteractiblePath = "res://assets/interactibles/run-history-scroll.png";
	public static readonly string MapInteractiblePath = "res://assets/interactibles/map.png";

	// ── Scene backgrounds ─────────────────────────────────────────────────────

	public static readonly string OverworldBackgroundPath = "res://assets/backgrounds/overworld/background.png";
	public static readonly string MapBackgroundPath = "res://assets/backgrounds/map/world-map.png";
	public static readonly string CampBackgroundPath = "res://assets/backgrounds/camp/camp.png";
}
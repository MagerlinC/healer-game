using System.Collections.Generic;

namespace healerfantasy;

public static class GameConstants
{
	/// <summary>
	/// The player character's name: shown in party frames, meters and combat logs, and
	/// used to identify the player in combat events and as the player's node name in World.tscn.
	/// </summary>
	public const string LightkeeperName = "Lightkeeper";


	// Party members (formerly the Templar, Assassin and Wizard).
	public const string DawnwardenName = "Dawnwarden";
	public const string DuskbladeName = "Duskblade";
	public const string StarcallerName = "Starcaller";

	// ── Boss names — Ancient Keep ─────────────────────────────────────────────
	public const int AncientKeepTier = 1;
	public const string Boss1Name = "Crystal Knight";
	public const string Boss2Name = "Bringer of Death";
	public const string Boss3Name = "Demon Slime";

	// ── Boss names — Forsaken Citadel ─────────────────────────────────────────
	public const int ForsakenCitadelTier = 2;
	public const string ForsakenBoss1Name = "Flying Demon";
	public const string ForsakenBoss2Name = "Mecha Golem";
	public const string ForsakenBoss3Name = "Flying Skull";

	// ── Boss names — Castle of Blood ─────────────────────────────────────────
	public const int CastleOfBloodTier = 2;
	public const string CastleBoss1Name = "Blood Knight";
	public const string CastleBoss2Name = "The Countess";
	public const string CastleBoss3Name = "The Blood Prince";

	// ── Boss names — Sanctum of Stars ─────────────────────────────────────────
	public const int SanctumOfStarsTier = 3;
	public const string SanctumBoss1Name = "The Nightborne";

	/// <summary>Primary (Dawn) twin — matches the CharacterName used on that node.</summary>
	public const string SanctumBoss2Name = "Astral Twin (Dawn)";

	public const string SanctumBoss3Name = "That Which Swallowed the Stars";

	// ── Boss names — The Frozen Peak ──────────────────────────────────────────
	/// <summary>Tier 4 — a single-boss final dungeon accessible after clearing all Tier 3 dungeons.</summary>
	public const int FrozenPeakTier = 4;

	public const string FrozenPeakBossName = "Queen of the Frozen Wastes";
	public const string FrozenTerrorName = "The Frozen Terror";

	// ── Boss names — The Abyss ────────────────────────────────────────────────
	/// <summary>
	/// Tier 5 — a secret final dungeon past the Frozen Peak, only part of a run while
	/// the Rune of the Abyss is active.
	/// </summary>
	public const int AbyssTier = 5;

	public const string AbyssDungeonName = "The Abyss";
	public const string AbyssBossName = "The Infinite Abyss";


	public const string BossGroupName = "boss";
	public const string PartyGroupName = "party";

	public static Dictionary<int, List<int>> BossHealthBaseValuesByDungeonTier = new()
	{
		{ 1, new List<int> { 800, 1000, 1200 } },
		{ 2, new List<int> { 1400, 2000, 2200 } },
		{ 3, new List<int> { 2400, 2800, 3000 } },
		{ 4, new List<int> { 5000 } },
		{ 5, new List<int> { 9000 } }
	};

	public static float InfiniteDuration = 999999f;

	/// <summary>Packed scene paths for the Ancient Keep bosses.</summary>
	public static readonly string[] BossScenePaths =
	{
		"res://levels/CrystalKnight.tscn",
		"res://levels/BringerOfDeath.tscn",
		"res://levels/DemonSlime.tscn"
	};

	/// <summary>Packed scene paths for the Forsaken Citadel bosses.</summary>
	public static readonly string[] ForsakenCitadelBossScenePaths =
	{
		"res://levels/FlyingDemon.tscn",
		"res://levels/MechaGolem.tscn",
		"res://levels/FlyingSkull.tscn"
	};

	/// <summary>Packed scene paths for the Castle of Blood bosses.</summary>
	public static readonly string[] CastleOfBloodBossScenePaths =
	{
		"res://levels/BloodKnight.tscn",
		"res://levels/TheCountess.tscn",
		"res://levels/TheBloodPrince.tscn"
	};


	/// <summary>Packed scene paths for the Sanctum of Stars bosses.</summary>
	public static readonly string[] SanctumBossScenePaths =
	{
		"res://levels/TheNightborne.tscn",
		"res://levels/AstralTwins.tscn",
		"res://levels/ThatWhichSwallowedTheStars.tscn"
	};


	/// <summary>
	/// Elliptical arena boundary for the Frozen Peak, expressed as fractions of
	/// the viewport's world-space half-width (X) and half-height (Y).
	/// This mirrors how <c>AddArenaBounds</c> computes wall positions, so the
	/// boundary scales correctly at any resolution or camera zoom.
	///
	/// 1.0 = exactly the screen edge. Make X larger than Y for the 2-D perspective
	/// foreshortening (arena looks wider than it is tall). Tune in-game with
	/// Debug → Visible Collision Shapes enabled.
	/// </summary>
	public const float FrozenPeakArenaFractionX = 0.72f; // horizontal semi-axis 

	public const float FrozenPeakArenaFractionY = 0.60f; // vertical semi-axis 

	/// <summary>
	/// How far to shift the ellipse centre downward, as a fraction of the
	/// viewport's total world-space height (0 = screen centre, 1 = bottom edge).
	/// 0.20 moves the ring down by 20 % of the screen height.
	/// </summary>
	public const float FrozenPeakArenaCenterOffsetY = 0.14f;

	/// <summary>Packed scene path for the Frozen Peak — a single final boss encounter.</summary>
	public static readonly string[] FrozenPeakBossScenePaths =
	{
		"res://levels/QueenOfTheFrozenWastes.tscn"
	};

	/// <summary>Packed scene path for The Abyss — the secret final encounter.</summary>
	public static readonly string[] AbyssBossScenePaths =
	{
		"res://levels/TheInfiniteAbyss.tscn"
	};

	// ── Rune system ───────────────────────────────────────────────────────────

	/// <summary>
	/// Per-active-rune health multiplier bonus applied to all bosses.
	/// E.g. with 3 runes active, boss MaxHealth is multiplied by 1.30.
	/// </summary>
	public const float RuneHealthBonusPerRune = 0.10f;

	/// <summary>Rune 3 (Midnight): bosses cast all abilities this much faster (1.10 = 10% haste).</summary>
	public const float RuneMidnightHasteMultiplier = 1.10f;

	/// <summary>Rune 1 (Twilight): fraction of damage dealt that becomes a Shroud (light absorption).</summary>
	public const float RuneTwilightShroudFraction = 0.20f;

	/// <summary>
	/// Rune 2 (Nightfall): seconds between each thorns spawn during a boss fight.
	/// </summary>
	public const float RuneNightfallThornsInterval = 8f;

	/// <summary>Rune 2 (Nightfall): damage the thorns deal to their target per second.</summary>
	public const float RuneNightfallThornsDamagePerSecond = 10f;

	public const float RuneNightfallThornsMaxHealthPerDungeonTier = 50f;

	/// <summary>Group name used for Thorns enemies so they can be found by the UI.</summary>
	public const string ThornsGroupName = "thorns";

}
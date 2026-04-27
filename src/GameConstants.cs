namespace healerfantasy;

public static class GameConstants
{
	public const string HealerName = "Healer";
	public const string TemplarName = "Templar";
	public const string AssassinName = "Assassin";
	public const string WizardName = "Wizard";

	// ── Boss names — Ancient Keep ─────────────────────────────────────────────
	public const string Boss1Name = "Crystal Knight";
	public const string Boss2Name = "Bringer of Death";
	public const string Boss3Name = "Demon Slime";

	// ── Boss names — Forsaken Citadel ─────────────────────────────────────────
	public const string ForsakenBoss1Name = "Flying Demon";
	public const string ForsakenBoss2Name = "Mecha Golem";
	public const string ForsakenBoss3Name = "Flying Skull";

	// ── Boss names — Sanctum of Stars ─────────────────────────────────────────
	public const string SanctumBoss1Name = "The Nightborne";

	/// <summary>Primary (Dawn) twin — matches the CharacterName used on that node.</summary>
	public const string SanctumBoss2Name = "Astral Twin (Dawn)";

	public const string SanctumBoss3Name = "That Which Swallowed the Stars";

	public const string BossGroupName = "boss";

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

	/// <summary>Packed scene paths for the Sanctum of Stars bosses.</summary>
	public static readonly string[] SanctumBossScenePaths =
	{
		"res://levels/TheNightborne.tscn",
		"res://levels/AstralTwins.tscn",
		"res://levels/ThatWhichSwallowedTheStars.tscn"
	};

	// ── XP rewards ────────────────────────────────────────────────────────────

	/// <summary>
	/// XP awarded for defeating each boss, indexed by encounter (0–2).
	/// Crystal Knight awards more XP to encourage repeated practice runs.
	/// </summary>
	public static readonly int[] BossXpRewards = { 200, 100, 150 };

	/// <summary>XP rewards for the harder Sanctum of Stars bosses.</summary>
	public static readonly int[] SanctumBossXpRewards = { 250, 300, 400 };
}
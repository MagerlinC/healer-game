namespace healerfantasy;

public static class GameConstants
{
	public const string PlayerName = "Healer";
	public const string TemplarName = "Templar";
	public const string AssassinName = "Assassin";
	public const string WizardName = "Wizard";

	public const string Boss1Name = "Crystal Knight";
	public const string Boss2Name = "Bringer of Death";
	public const string Boss3Name = "Demon Slime";

	public const string BossGroupName = "boss";

	/// <summary>Packed scene paths for bosses, ordered by encounter index.</summary>
	public static readonly string[] BossScenePaths =
	{
		"res://levels/CrystalKnight.tscn",
		"res://levels/BringerOfDeath.tscn",
		"res://levels/DemonSlime.tscn"
	};

	// ── XP rewards ────────────────────────────────────────────────────────────

	/// <summary>
	/// XP awarded for defeating each boss, indexed by encounter (0–2).
	/// Crystal Knight awards more XP to encourage repeated practice runs.
	/// </summary>
	public static readonly int[] BossXpRewards = { 200, 100, 150 };

}
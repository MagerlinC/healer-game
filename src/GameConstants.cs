namespace healerfantasy;

public static class GameConstants
{
	public const string PlayerName = "Healer";

	public const string Boss1Name = "Crystal Knight";
	public const string Boss2Name = "Bringer of Death";
	public const string Boss3Name = "Demon Slime";

	public const string BossGroupName = "boss";

	/// <summary>Packed scene paths for bosses, ordered by encounter index.</summary>
	public static readonly string[] BossScenePaths =
	{
		"res://levels/CrystalKnight.tscn",
		"res://levels/BringerOfDeath.tscn",
		"res://levels/DemonSlime.tscn",
	};

	/// <summary>Arena background image paths, one per boss encounter.</summary>
	public static readonly string[] ArenaBackgroundPaths =
	{
		"res://assets/backgrounds/space/1.png",
		"res://assets/backgrounds/space/2.png",
		"res://assets/backgrounds/space/3.png",
	};
}
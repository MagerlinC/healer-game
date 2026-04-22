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
		"res://levels/DemonSlime.tscn"
	};

}
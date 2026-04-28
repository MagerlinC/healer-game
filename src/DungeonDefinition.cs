#nullable enable
using System;
using System.Collections.Generic;
using Godot;

namespace healerfantasy;

/// <summary>
/// Defines a dungeon — its display name, position on the world map, and the
/// ordered list of boss encounters it contains.
/// </summary>
public class DungeonDefinition
{
	public string Name { get; init; } = "";

	/// <summary>
	/// Tier determines which slot in the run this dungeon occupies (1 = first, 2 = second, 3 = third).
	/// One dungeon per tier is selected randomly when a run is started.
	/// </summary>
	public int Tier { get; init; }

	/// <summary>Centre of this dungeon's node on the world map (viewport pixels, 1920×1080).</summary>
	public Vector2 MapPosition { get; init; }

	public string[] BossScenePaths { get; init; } = Array.Empty<string>();
	public string[] ArenaBackgroundPaths { get; init; } = Array.Empty<string>();
	public List<int> XpRewards { get; init; } = [];

	/// <summary>
	/// Display names for each boss encounter, used by <see cref="RunState.CurrentBossName"/>
	/// and the boss health bar. When empty the system falls back to the
	/// <see cref="GameConstants.Boss1Name"/> / Boss2Name / Boss3Name defaults.
	/// </summary>
	public string[] BossNames { get; init; } = Array.Empty<string>();

	/// <summary>Number of boss encounters in this dungeon.</summary>
	public int BossCount => BossScenePaths.Length;

	// ── Dungeon catalogue ─────────────────────────────────────────────────────

	public static readonly DungeonDefinition[] All =
	[
		new()
		{
			Name = "The Ancient Keep",
			Tier = GameConstants.AncientKeepTier,
			MapPosition = new Vector2(260f, 730f),
			BossScenePaths = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.AncientKeepArenaBackgroundPaths,
			XpRewards = GameConstants.XpRewardsByDungeonTier[GameConstants.AncientKeepTier],
			BossNames = new[]
			{
				GameConstants.Boss1Name,
				GameConstants.Boss2Name,
				GameConstants.Boss3Name
			}
		},
		new()
		{
			Name = "The Forsaken Citadel",
			Tier = GameConstants.ForsakenCitadelTier,
			MapPosition = new Vector2(880f, 490f),
			BossScenePaths = GameConstants.ForsakenCitadelBossScenePaths,
			ArenaBackgroundPaths = AssetConstants.ForsakenCitadelArenaBackgroundPaths,
			XpRewards = GameConstants.XpRewardsByDungeonTier[GameConstants.ForsakenCitadelTier],
			BossNames = new[]
			{
				GameConstants.ForsakenBoss1Name, // "Flying Demon"
				GameConstants.ForsakenBoss2Name, // "Mecha Golem"
				GameConstants.ForsakenBoss3Name // "Flying Skull"
			}
		},
		new()
		{
			Name = "Castle of Blood",
			Tier = GameConstants.CastleOfBloodTier,
			MapPosition = new Vector2(880f, 490f),
			BossScenePaths = GameConstants.CastleOfBloodBossScenePaths,
			ArenaBackgroundPaths = AssetConstants.CastleOfBloodArenaBackgroundPaths,
			XpRewards = GameConstants.XpRewardsByDungeonTier[GameConstants.CastleOfBloodTier],
			BossNames = new[]
			{
				GameConstants.CastleBoss1Name, // "Blood Knight"
				GameConstants.CastleBoss2Name, // "The Countess"
				GameConstants.CastleBoss3Name // "The Blood Prince"
			}
		},
		new()
		{
			Name = "The Sanctum of Stars",
			Tier = GameConstants.SanctumOfStarsTier,
			MapPosition = new Vector2(1540f, 240f),
			BossScenePaths = GameConstants.SanctumBossScenePaths,
			ArenaBackgroundPaths = AssetConstants.SpaceArenaBackgroundPaths,
			XpRewards = GameConstants.XpRewardsByDungeonTier[GameConstants.SanctumOfStarsTier],
			BossNames = new[]
			{
				GameConstants.SanctumBoss1Name, // "The Nightborne"
				GameConstants.SanctumBoss2Name, // "Astral Twin (Dawn)"  — primary twin for health bar
				GameConstants.SanctumBoss3Name // "That Which Swallowed the Stars"
			}
		}
	];

	/// <summary>
	/// Map positions for the two rest camps between dungeons.
	/// Camp[0] sits between dungeon 0 and 1; Camp[1] between dungeon 1 and 2.
	/// </summary>
	public static readonly Vector2[] CampMapPositions =
	[
		new(575f, 610f),
		new(1215f, 370f)
	];
}
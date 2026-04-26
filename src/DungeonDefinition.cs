#nullable enable
using System;
using Godot;

namespace healerfantasy;

/// <summary>
/// Defines a dungeon — its display name, position on the world map, and the
/// ordered list of boss encounters it contains.
/// </summary>
public class DungeonDefinition
{
	public string Name { get; init; } = "";

	/// <summary>Centre of this dungeon's node on the world map (viewport pixels, 1920×1080).</summary>
	public Vector2 MapPosition { get; init; }

	public string[] BossScenePaths { get; init; } = Array.Empty<string>();
	public string[] ArenaBackgroundPaths { get; init; } = Array.Empty<string>();
	public int[] XpRewards { get; init; } = Array.Empty<int>();

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
			Name = "The Sanctum of Stars",
			MapPosition = new Vector2(1540f, 240f),
			BossScenePaths = GameConstants.SanctumBossScenePaths,
			ArenaBackgroundPaths = AssetConstants.SpaceArenaBackgroundPaths,
			XpRewards = GameConstants.SanctumBossXpRewards,
			BossNames = new[]
			{
				GameConstants.SanctumBoss1Name, // "The Nightborne"
				GameConstants.SanctumBoss2Name, // "Astral Twin (Dawn)"  — primary twin for health bar
				GameConstants.SanctumBoss3Name // "That Which Swallowed the Stars"
			}
		},
		new()
		{
			Name = "The Ancient Keep",
			MapPosition = new Vector2(260f, 730f),
			BossScenePaths = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.AncientKeepArenaBackgroundPaths,
			XpRewards = GameConstants.BossXpRewards
			// BossNames not set — RunState falls back to Boss1/2/3Name defaults.
		},
		new()
		{
			Name = "The Forsaken Citadel",
			MapPosition = new Vector2(880f, 490f),
			BossScenePaths = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.ForsakenCitadelArenaBackgroundPaths,
			XpRewards = GameConstants.BossXpRewards
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
#nullable enable
using System;
using Godot;

namespace healerfantasy;

/// <summary>
/// Defines a dungeon — its display name, position on the world map, and the
/// ordered list of boss encounters it contains.
///
/// All three entries currently share the same boss roster; swap out
/// <see cref="BossScenePaths"/>, <see cref="ArenaBackgroundPaths"/>, and
/// <see cref="XpRewards"/> per-entry as new unique dungeons are built.
/// </summary>
public class DungeonDefinition
{
	public string Name { get; init; } = "";

	/// <summary>Centre of this dungeon's node on the world map (viewport pixels, 1920×1080).</summary>
	public Vector2 MapPosition { get; init; }

	public string[] BossScenePaths { get; init; } = Array.Empty<string>();
	public string[] ArenaBackgroundPaths { get; init; } = Array.Empty<string>();
	public int[] XpRewards { get; init; } = Array.Empty<int>();

	/// <summary>Number of boss encounters in this dungeon.</summary>
	public int BossCount => BossScenePaths.Length;

	// ── Dungeon catalogue ─────────────────────────────────────────────────────

	/// <summary>
	/// The three dungeons used in a run, ordered by encounter.
	/// All share the same boss roster for now; this is where you add new dungeons later.
	/// Map positions are approximate for a 1920×1080 viewport — tweak after testing.
	/// </summary>
	public static readonly DungeonDefinition[] All =
	[
		new()
		{
			Name                 = "The Ancient Keep",
			MapPosition          = new Vector2(260f, 730f),
			BossScenePaths       = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.ArenaBackgroundPaths,
			XpRewards            = GameConstants.BossXpRewards,
		},
		new()
		{
			Name                 = "The Forsaken Citadel",
			MapPosition          = new Vector2(880f, 490f),
			BossScenePaths       = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.ArenaBackgroundPaths,
			XpRewards            = GameConstants.BossXpRewards,
		},
		new()
		{
			Name                 = "The Void Sanctum",
			MapPosition          = new Vector2(1540f, 240f),
			BossScenePaths       = GameConstants.BossScenePaths,
			ArenaBackgroundPaths = AssetConstants.ArenaBackgroundPaths,
			XpRewards            = GameConstants.BossXpRewards,
		},
	];

	/// <summary>
	/// Map positions for the two rest camps between dungeons.
	/// Camp[0] sits between dungeon 0 and 1; Camp[1] between dungeon 1 and 2.
	/// </summary>
	public static readonly Vector2[] CampMapPositions =
	[
		new Vector2(575f, 610f),
		new Vector2(1215f, 370f),
	];
}

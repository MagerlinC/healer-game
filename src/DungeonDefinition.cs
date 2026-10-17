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
	/// In-world flavour text shown on the world map: what is known of the place and
	/// hints of what lurks inside, without naming its bosses outright.
	/// </summary>
	public string Description { get; init; } = "";

	/// <summary>
	/// Tier determines which slot in the run this dungeon occupies (1 = first, 2 = second, 3 = third).
	/// One dungeon per tier is selected randomly when a run is started.
	/// </summary>
	public int Tier { get; init; }

	/// <summary>Centre of this dungeon's node on the world map (viewport pixels, 1920×1080).</summary>
	public Vector2 MapPosition { get; init; }

	public string[] BossScenePaths { get; init; } = Array.Empty<string>();

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
			Description = "Its halls were sealed when the light went out, and its sentinel never left its post: a knight grown over with cold crystal. Deeper in, something drags a scythe through the dark, and in the lowest cellar something wet and hungry keeps growing.",
			Tier = GameConstants.AncientKeepTier,
			MapPosition = new Vector2(260f, 730f),
			BossScenePaths = GameConstants.BossScenePaths,
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
			Description = "A fortress abandoned by those who built it, but not by what they made. Wings beat in its broken towers, an iron engine still grinds in the forge below, and a grinning skull drifts the halls, burning with a fire that gives no warmth.",
			Tier = GameConstants.ForsakenCitadelTier,
			MapPosition = new Vector2(880f, 490f),
			BossScenePaths = GameConstants.ForsakenCitadelBossScenePaths,
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
			Description = "Candles still burn in the castle windows, though no one living lights them. A knight in armour rusted red keeps the gate, the Countess holds court for guests who never leave, and her heir waits in the high tower, thirsting.",
			Tier = GameConstants.CastleOfBloodTier,
			MapPosition = new Vector2(880f, 490f),
			BossScenePaths = GameConstants.CastleOfBloodBossScenePaths,
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
			Description = "Once an observatory, where scholars charted the stars until they went out one by one. Something born of the night keeps vigil beneath the dome, two lights circle each other endlessly in the dark, and above it all waits the hunger that ate the sky.",
			Tier = GameConstants.SanctumOfStarsTier,
			MapPosition = new Vector2(1540f, 240f),
			BossScenePaths = GameConstants.SanctumBossScenePaths,
			BossNames = new[]
			{
				GameConstants.SanctumBoss1Name, // "The Nightborne"
				GameConstants.SanctumBoss2Name, // "Astral Twin (Dawn)"  — primary twin for health bar
				GameConstants.SanctumBoss3Name // "That Which Swallowed the Stars"
			}
		},
		new()
		{
			Name = "The Guttering Spire",
			Description = "The last candle at the edge of the world, burned down into a spire of its own wax. A queen sits on a throne of drippings and hoards what is left of the light, and she means to snuff out yours.",
			Tier = GameConstants.GutteringSpireTier,
			MapPosition = new Vector2(960f, 120f), // centre-top of the world map — the final destination
			BossScenePaths = GameConstants.GutteringSpireBossScenePaths,
			BossNames = new[]
			{
				GameConstants.GutteringSpireBossName // "The Tallow Queen"
			}
		},
		new()
		{
			// Only part of a run while the Rune of the Abyss is active (see RunState.RunDungeons).
			Name = GameConstants.AbyssDungeonName,
			Description = "Past the peak, the world simply stops. Beyond the edge lies a darkness with no floor, and it is not empty. It has watched the light dwindle for a long time, and it is patient.",
			Tier = GameConstants.AbyssTier,
			MapPosition = new Vector2(1180f, 150f),
			BossScenePaths = GameConstants.AbyssBossScenePaths,
			BossNames = new[] { GameConstants.AbyssBossName }
		}
	];
}
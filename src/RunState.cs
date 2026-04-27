#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.Items;
using healerfantasy.SpellResources;
using healerfantasy.SpellResources.Chronomancy;
using healerfantasy.Talents;

namespace healerfantasy;

/// <summary>
/// Autoload singleton that persists run-level state across scenes.
///
/// Tracks spell/talent loadout AND the overall run progression through the
/// dungeon → camp → dungeon → camp → dungeon sequence.
///
/// Key properties used by World.cs:
///   <see cref="CurrentDungeon"/>    — which dungeon we're currently fighting
///   <see cref="CurrentBossIndexInDungeon"/> — which boss within that dungeon
///
/// Key transitions:
///   <see cref="AdvanceBossInDungeon"/> — mid-dungeon boss cleared (not the last one)
///   <see cref="CompleteDungeon"/>      — last boss of a dungeon cleared
///   <see cref="CompleteCamp"/>         — player leaves camp for the world map
/// </summary>
public partial class RunState : Node
{
	public static RunState Instance { get; private set; } = null!;

	// ── Loadout ───────────────────────────────────────────────────────────────

	/// <summary>The 6 spells the player chose. Null slots are empty.</summary>
	public SpellResource?[] SelectedSpells { get; private set; } = new SpellResource?[Player.MaxSpellSlots];

	/// <summary>Talent definitions selected by the player.</summary>
	public List<TalentDefinition> SelectedTalentDefs { get; } = new();

	public bool HasLoadout => SelectedSpells.Any(s => s != null);

	// ── Run dungeon list ──────────────────────────────────────────────────────

	/// <summary>
	/// The ordered list of dungeons for this run, one randomly chosen per tier.
	/// Populated at run start and on Reset(). Always contains exactly one dungeon
	/// per tier (1, 2, 3) in ascending tier order.
	/// </summary>
	public List<DungeonDefinition> RunDungeons { get; private set; } = null!;

	/// <summary>Picks one dungeon per tier at random and returns them in tier order.</summary>
	static List<DungeonDefinition> BuildRunDungeons()
	{
		var rng = new Random();
		return DungeonDefinition.All
			.GroupBy(d => d.Tier)
			.OrderBy(g => g.Key)
			.Select(g =>
			{
				var options = g.ToList();
				return options[rng.Next(options.Count)];
			})
			.ToList();
	}

	// ── Run progression ───────────────────────────────────────────────────────

	/// <summary>How many dungeons have been fully cleared this run (0–3).</summary>
	public int CompletedDungeons { get; private set; } = 0;

	/// <summary>How many rest camps have been departed this run (0–2).</summary>
	public int CompletedCamps { get; private set; } = 0;

	/// <summary>Which boss within the current dungeon is up next (0-based).</summary>
	public int CurrentBossIndexInDungeon { get; private set; } = 0;

	// ── Derived ───────────────────────────────────────────────────────────────

	/// <summary>Index of the dungeon currently being fought (equals CompletedDungeons).</summary>
	public int CurrentDungeonIndex => CompletedDungeons;

	/// <summary>Backward-compat alias — World.cs and DeathScreen still reference this.</summary>
	public int CurrentBossIndex => CurrentBossIndexInDungeon;

	/// <summary>
	/// Display name of the current boss.
	/// Uses the dungeon's explicit <see cref="DungeonDefinition.BossNames"/> array when
	/// available; falls back to the shared Boss1/2/3Name constants for older dungeons
	/// that don't define per-boss names.
	/// </summary>
	public string CurrentBossName
	{
		get
		{
			var names = CurrentDungeon.BossNames;
			if (names != null && names.Length > CurrentBossIndexInDungeon)
				return names[CurrentBossIndexInDungeon];

			return CurrentBossIndexInDungeon switch
			{
				0 => GameConstants.Boss1Name,
				1 => GameConstants.Boss2Name,
				2 => GameConstants.Boss3Name,
				_ => "Unknown"
			};
		}
	}

	/// <summary>Definition of the dungeon currently being fought or about to be entered.</summary>
	public DungeonDefinition CurrentDungeon =>
		RunDungeons[Math.Min(CurrentDungeonIndex, RunDungeons.Count - 1)];

	/// <summary>True when the dungeon we're in is the final one (no camp follows).</summary>
	public bool IsLastDungeon => CurrentDungeonIndex >= RunDungeons.Count - 1;

	/// <summary>True when the boss we're about to fight is the last one in the current dungeon.</summary>
	public bool IsLastBossInDungeon => CurrentBossIndexInDungeon >= CurrentDungeon.BossCount - 1;

	// ── Map node state ────────────────────────────────────────────────────────

	public enum MapNodeState { Locked, Available, InProgress, Completed }

	/// <summary>
	/// Visual state of dungeon node [index] on the world map.
	/// A dungeon is Available when it is the next to fight and either (a) no camp
	/// is pending yet (start of run) or (b) the camp before it is currently in
	/// progress (player is at camp and hasn't departed yet).
	/// </summary>
	public MapNodeState GetDungeonMapState(int index)
	{
		if (index < CompletedDungeons) return MapNodeState.Completed;
		if (index == CompletedDungeons && index < RunDungeons.Count)
		{
			// Available if either we have no pending camp, or we're currently sitting
			// at the camp that bridges to this dungeon (CompletedCamps == index - 1).
			if (CompletedCamps >= CompletedDungeons - 1)
				return MapNodeState.Available;
		}
		return MapNodeState.Locked;
	}

	/// <summary>Visual state of camp node [index] on the world map.</summary>
	public MapNodeState GetCampMapState(int index)
	{
		if (index < CompletedCamps) return MapNodeState.Completed;
		// InProgress = dungeon before this camp is done but player hasn't departed yet
		if (index == CompletedCamps && CompletedDungeons > CompletedCamps) return MapNodeState.InProgress;
		return MapNodeState.Locked;
	}

	// ── State transitions ─────────────────────────────────────────────────────

	/// <summary>
	/// Call when a non-final boss in the current dungeon is defeated.
	/// Advances to the next boss without leaving the dungeon.
	/// </summary>
	public void AdvanceBossInDungeon() => CurrentBossIndexInDungeon++;

	/// <summary>
	/// Call when the LAST boss of a non-final dungeon is defeated.
	/// Marks the dungeon as completed and resets the intra-dungeon boss index
	/// so the next dungeon will start at boss 0.
	/// </summary>
	public void CompleteDungeon()
	{
		CompletedDungeons++;
		CurrentBossIndexInDungeon = 0;
	}

	/// <summary>
	/// Call when the player departs from a rest camp (clicks the map on MapScreen).
	/// Increments CompletedCamps so the next dungeon appears Available on the map.
	/// </summary>
	public void CompleteCamp() => CompletedCamps++;

	// ── Lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		Instance = this;
		RunDungeons = BuildRunDungeons();
		InitSpellsFromPreferences();
		InitTalentsFromPreferences();
	}

	// ── Public API ────────────────────────────────────────────────────────────

	public void SetSpells(SpellResource?[] spells)
	{
		for (var i = 0; i < Player.MaxSpellSlots; i++)
			SelectedSpells[i] = i < spells.Length ? spells[i] : null;
	}

	public void SetTalents(IEnumerable<TalentDefinition> defs)
	{
		SelectedTalentDefs.Clear();
		SelectedTalentDefs.AddRange(defs);
	}

	/// <summary>Resets all run progression for a completely fresh run.</summary>
	public void Reset()
	{
		CompletedDungeons = 0;
		CompletedCamps = 0;
		CurrentBossIndexInDungeon = 0;
		ItemStore.Clear();
		RunDungeons = BuildRunDungeons();
	}

	// ── Private ───────────────────────────────────────────────────────────────

	void InitSpellsFromPreferences()
	{
		if (LoadoutPreferences.HasSavedSpells)
			SetSpells(LoadoutPreferences.SavedSpells);
		else
			InitDefaultSpells();
	}

	void InitDefaultSpells()
	{
		SelectedSpells =
		[
			new TouchOfLightSpell(), new WaveOfIncandescenceSpell(), new WildGrowthSpell(),
			new RewindSpell(), new TimeWarpSpell(), new DecaySpellResource()
		];
	}

	void InitTalentsFromPreferences()
	{
		if (LoadoutPreferences.HasSavedTalents)
			SetTalents(LoadoutPreferences.SavedTalents);
	}
}

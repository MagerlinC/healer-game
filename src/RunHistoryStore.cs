using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using healerfantasy.CombatLog;
using healerfantasy.Items;
using healerfantasy.Runes;

namespace healerfantasy;

/// <summary>
/// In-memory store of completed run records.
/// Persists for the lifetime of the process; resets on game restart.
///
/// Workflow:
///   1. Call <see cref="StartRun"/> when the player begins a new run
///      (from the Overworld or when retrying after a wipe).
///   2. Call <see cref="RecordBossEncounter"/> after each boss is defeated.
///      This snapshots the current <see cref="CombatLog"/> for that encounter.
///   3. Call <see cref="FinalizeRun"/> when the run ends (victory or wipe).
/// </summary>
public static class RunHistoryStore
{
	// ── data types ────────────────────────────────────────────────────────────
	const string FileSavePath = "user://run-history.save";

	static readonly List<string> PartyNames =
		[GameConstants.HealerName, GameConstants.WizardName, GameConstants.AssassinName, GameConstants.TemplarName];

	public sealed record BossEncounterRecord(
		string BossName,
		float TotalHealing,
		float TotalDamageDealt,
		float TotalDamageTaken,
		List<CombatEventRecord> Events,
		/// <summary>
		/// The dungeon this encounter took place in.
		/// Null on records saved before dungeon tracking was introduced.
		/// </summary>
		string? DungeonName = null
	);


	public sealed record RunRecord(
		bool IsVictory,
		long DurationTicks,
		List<BossEncounterRecord> BossEncounters,
		DateTime CompletedAt,
		/// <summary>
		/// Display names of items equipped at the time the run was finalised.
		/// Null on records saved before the item system was introduced.
		/// </summary>
		List<string>? ItemsUsed = null,
		/// <summary>
		/// Indices (1-based, matching <see cref="RuneIndex"/>) of runes that
		/// were active when the run started.  Null on records saved before
		/// the rune system was introduced.
		/// </summary>
		List<int>? ActiveRuneIndices = null
	)
	{
		public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);
	};

	// ── state ─────────────────────────────────────────────────────────────────

	static readonly List<RunRecord> _history = LoadRunHistoryFromDisk();
	static readonly List<BossEncounterRecord> _currentEncounters = new();
	static DateTime _runStartTime = default;

	public static IReadOnlyList<RunRecord> History => _history.AsReadOnly();

	/// <summary>
	/// All boss encounters recorded in the current (in-progress) run.
	/// Updated after each <see cref="RecordBossEncounter"/> call.
	/// </summary>
	public static IReadOnlyList<BossEncounterRecord> CurrentRunEncounters => _currentEncounters.AsReadOnly();

	// Storing and loading from file on disk

	static List<RunRecord> LoadRunHistoryFromDisk()
	{
		if (!FileAccess.FileExists(FileSavePath))
			return [];

		using var saveFile = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Read);
		var jsonString = saveFile.GetAsText();

		return JsonSerializer.Deserialize<List<RunRecord>>(jsonString) ?? [];
	}

	static void WriteRunHistoryRecordToSaveFile(RunRecord record)
	{
		var existingHistory = LoadRunHistoryFromDisk();
		existingHistory.Add(record);
		using var saveFile = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Write);
		var json = JsonSerializer.Serialize(existingHistory);
		saveFile.StoreLine(json);
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Begin tracking a new run. Resets any pending encounter data.
	/// Call from the Overworld when "Start Run" is pressed, and from the
	/// DeathScreen when the player retries.
	/// </summary>
	public static void StartRun()
	{
		_runStartTime = DateTime.Now;
		_currentEncounters.Clear();
	}

	/// <summary>
	/// Snapshot the current <see cref="CombatLog"/> and store it as a
	/// boss encounter record.  Call when a boss is defeated (before
	/// <see cref="CombatLog.Clear"/> is called for the next encounter).
	/// Also call when the party wipes to record the failed attempt.
	/// </summary>
	public static void RecordBossEncounter(string bossName)
	{
		var events = CombatLog.CombatLog.Snapshot();
		var healing = 0f;
		var damageDealt = 0f;
		var damageTaken = 0f;

		foreach (var e in events)
		{
			if (e.Type == CombatEventType.Healing) healing += e.Amount;
			else
			{
				if (PartyNames.Contains(e.SourceName)) damageDealt += e.Amount;
				else damageTaken += e.Amount;
			}
		}

		var dungeonName = RunState.Instance?.CurrentDungeon.Name;
		_currentEncounters.Add(new BossEncounterRecord(bossName, healing, damageDealt, damageTaken, events, dungeonName));
	}

	/// <summary>
	/// Finalise the current run and append it to <see cref="History"/>.
	/// Safe to call even if <see cref="StartRun"/> was never called (no-op).
	/// </summary>
	public static void FinalizeRun(bool isVictory)
	{
		if (_runStartTime == default) return;

		var duration = DateTime.Now - _runStartTime;

		// Snapshot which runes were active at run end (before any state reset).
		var activeRuneIndices = Enumerable.Range(1, RuneStore.TotalRunes)
			.Where(i => RunState.Instance.IsRuneActive((RuneIndex)i))
			.ToList();

		var historyRecord =
			new RunRecord(
				isVictory,
				duration.Ticks,
				new List<BossEncounterRecord>(_currentEncounters),
				DateTime.Now,
				ItemStore.GetEquippedItemNames(),
				activeRuneIndices);
		_history.Add(historyRecord);
		WriteRunHistoryRecordToSaveFile(historyRecord);

		_runStartTime = default;
		_currentEncounters.Clear();

		// Items are run-scoped and lost after the run ends.
		ItemStore.Clear();
	}

	/// <summary>
	/// Deletes the run history save file from disk and clears the in-memory
	/// history list. Any in-progress run tracking is also reset.
	/// </summary>
	public static void DeleteSaveFile()
	{
		if (FileAccess.FileExists(FileSavePath))
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FileSavePath));
		_history.Clear();
		_currentEncounters.Clear();
		_runStartTime = default;
	}
}
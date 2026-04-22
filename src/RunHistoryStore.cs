using System;
using System.Collections.Generic;
using healerfantasy.CombatLog;

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

	public sealed class BossEncounterRecord
	{
		public string BossName { get; }
		public float TotalHealing { get; }
		public float TotalDamage { get; }
		public IReadOnlyList<CombatEventRecord> Events { get; }

		public BossEncounterRecord(
			string bossName, float totalHealing, float totalDamage,
			List<CombatEventRecord> events)
		{
			BossName = bossName;
			TotalHealing = totalHealing;
			TotalDamage = totalDamage;
			Events = events.AsReadOnly();
		}
	}

	public sealed class RunRecord
	{
		public bool IsVictory { get; }
		public TimeSpan Duration { get; }
		public DateTime CompletedAt { get; }
		public IReadOnlyList<BossEncounterRecord> BossEncounters { get; }

		public RunRecord(bool isVictory, TimeSpan duration,
			List<BossEncounterRecord> encounters, DateTime completedAt)
		{
			IsVictory = isVictory;
			Duration = duration;
			BossEncounters = encounters.AsReadOnly();
			CompletedAt = completedAt;
		}
	}

	// ── state ─────────────────────────────────────────────────────────────────

	static readonly List<RunRecord> _history = new();
	static readonly List<BossEncounterRecord> _currentEncounters = new();
	static DateTime _runStartTime = default;

	public static IReadOnlyList<RunRecord> History => _history.AsReadOnly();

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
		var damage = 0f;

		foreach (var e in events)
		{
			if (e.Type == CombatEventType.Healing) healing += e.Amount;
			else damage += e.Amount;
		}

		_currentEncounters.Add(new BossEncounterRecord(bossName, healing, damage, events));
	}

	/// <summary>
	/// Finalise the current run and append it to <see cref="History"/>.
	/// Safe to call even if <see cref="StartRun"/> was never called (no-op).
	/// </summary>
	public static void FinalizeRun(bool isVictory)
	{
		if (_runStartTime == default) return;

		var duration = DateTime.Now - _runStartTime;
		_history.Add(new RunRecord(
			isVictory, duration,
			new List<BossEncounterRecord>(_currentEncounters),
			DateTime.Now));

		_runStartTime = default;
		_currentEncounters.Clear();
	}
}
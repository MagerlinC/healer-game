using System.Linq;
using System.Text.Json;
using Godot;

namespace healerfantasy;

/// <summary>
/// Persistent store for per-account flags that survive across runs:
/// tutorial seen state and spellbook opened state.
///
/// Saved to <c>user://player-progress.save</c> and survives game restarts.
/// </summary>
public static class PlayerProgressStore
{
	// ── constants ─────────────────────────────────────────────────────────────

	const string FileSavePath = "user://player-progress.save";

	// ── data ──────────────────────────────────────────────────────────────────

	public sealed class ProgressData
	{
		/// <summary>True once the player has dismissed the first-time tutorial popup.</summary>
		public bool HasSeenTutorial { get; set; } = false;

		/// <summary>True once the player has opened the Spellbook at least once.</summary>
		public bool HasOpenedSpellbook { get; set; } = false;

		// ── News Board entries ────────────────────────────────────────────────────

		/// <summary>
		/// Unlocked the first time the player accumulates ≥3 talent points in any
		/// single school during a run — reveals the Ultimate Abilities board entry.
		/// </summary>
		public bool HasUnlockedUltimateEntry { get; set; } = false;

		/// <summary>True once the player has read the Ultimate Abilities board entry.</summary>
		public bool HasSeenUltimateEntry { get; set; } = false;

		/// <summary>
		/// Unlocked the first time the player defeats the Queen of the Frozen Wastes
		/// (completes a full run) — reveals the Runes board entry.
		/// </summary>
		public bool HasUnlockedRuneEntry { get; set; } = false;

		/// <summary>True once the player has read the Runes board entry.</summary>
		public bool HasSeenRuneEntry { get; set; } = false;

		// ── In-combat tutorial overlays ───────────────────────────────────────────

		/// <summary>
		/// True once the player has seen (and dismissed) the Deflect tutorial overlay.
		/// Shown the first time a parryable boss spell begins winding up.
		/// </summary>
		public bool HasSeenDeflectTutorial { get; set; } = false;

		/// <summary>
		/// True once the player has seen (and dismissed) the Dispel tutorial overlay.
		/// Shown the first time a harmful, dispellable effect is applied to the party.
		/// </summary>
		public bool HasSeenDispelTutorial { get; set; } = false;

		/// <summary>
		/// True once the player has seen (and dismissed) the Detonation Zone tutorial overlay.
		/// Shown the first time a Detonation Zone is placed by the Demon Slime.
		/// </summary>
		public bool HasSeenDetonationTutorial { get; set; } = false;

		/// <summary>
		/// True once the player has seen (and dismissed) the targeting tutorial overlay.
		/// Shown at the start of the player's very first combat encounter.
		/// </summary>
		public bool HasSeenTargetingTutorial { get; set; } = false;

		/// <summary>
		/// True once the player has seen (and dismissed) the camp merchant tutorial overlay.
		/// Shown the first time the player visits a camp rest stop.
		/// </summary>
		public bool HasSeenCampMerchantTutorial { get; set; } = false;
	}

	/// <summary>
	/// True if the player has defeated The Blood Prince (the final boss of the
	/// Castle of Blood) in any previous run.  Unlocks the Sanguimancy spell school.
	///
	/// Derived from run history so no separate flag is needed — the history already
	/// persists to disk and survives game restarts.
	/// </summary>
	public static bool HasDefeatedCastleOfBlood =>
		RunHistoryStore.History.Any(r =>
			r.BossEncounters.Any(e => e.BossName == GameConstants.CastleBoss3Name));

	// ── in-memory state ───────────────────────────────────────────────────────

	static ProgressData _data = LoadFromDisk();

	/// <summary>True once the player has dismissed the first-time tutorial popup.</summary>
	public static bool HasSeenTutorial => _data.HasSeenTutorial;

	/// <summary>True once the player has opened the Spellbook at least once.</summary>
	public static bool HasOpenedSpellbook => _data.HasOpenedSpellbook;

	// ── News Board properties ─────────────────────────────────────────────────

	/// <summary>True once the Ultimate Abilities news entry has been unlocked.</summary>
	public static bool HasUnlockedUltimateEntry => _data.HasUnlockedUltimateEntry;

	/// <summary>True once the player has read the Ultimate Abilities news entry.</summary>
	public static bool HasSeenUltimateEntry => _data.HasSeenUltimateEntry;

	/// <summary>True once the Runes news entry has been unlocked.</summary>
	public static bool HasUnlockedRuneEntry => _data.HasUnlockedRuneEntry;

	/// <summary>True once the player has read the Runes news entry.</summary>
	public static bool HasSeenRuneEntry => _data.HasSeenRuneEntry;

	// ── In-combat tutorial overlay properties ─────────────────────────────────

	/// <summary>True once the Deflect in-combat tutorial overlay has been dismissed.</summary>
	public static bool HasSeenDeflectTutorial => _data.HasSeenDeflectTutorial;

	/// <summary>True once the Dispel in-combat tutorial overlay has been dismissed.</summary>
	public static bool HasSeenDispelTutorial => _data.HasSeenDispelTutorial;

	/// <summary>True once the Detonation Zone in-combat tutorial overlay has been dismissed.</summary>
	public static bool HasSeenDetonationTutorial => _data.HasSeenDetonationTutorial;

	/// <summary>True once the targeting tutorial overlay has been dismissed.</summary>
	public static bool HasSeenTargetingTutorial => _data.HasSeenTargetingTutorial;

	/// <summary>True once the camp merchant tutorial overlay has been dismissed.</summary>
	public static bool HasSeenCampMerchantTutorial => _data.HasSeenCampMerchantTutorial;

	/// <summary>
	/// True if there is at least one unlocked news board entry the player has not yet read.
	/// Used to show the exclamation mark above the News Board interactible.
	/// </summary>
	public static bool HasUnreadBoardEntries =>
		(HasUnlockedUltimateEntry && !HasSeenUltimateEntry) ||
		(HasUnlockedRuneEntry     && !HasSeenRuneEntry);

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>Marks the tutorial as seen and saves to disk (idempotent).</summary>
	public static void MarkTutorialSeen()
	{
		if (_data.HasSeenTutorial) return;
		_data.HasSeenTutorial = true;
		SaveToDisk();
	}

	/// <summary>Marks the Spellbook as having been opened and saves to disk (idempotent).</summary>
	public static void MarkSpellbookOpened()
	{
		if (_data.HasOpenedSpellbook) return;
		_data.HasOpenedSpellbook = true;
		SaveToDisk();
	}

	// ── News Board API ────────────────────────────────────────────────────────

	/// <summary>
	/// Unlocks the Ultimate Abilities news board entry.
	/// Called when the player first allocates ≥3 talent points in any school during a run.
	/// Idempotent.
	/// </summary>
	public static void UnlockUltimateEntry()
	{
		if (_data.HasUnlockedUltimateEntry) return;
		_data.HasUnlockedUltimateEntry = true;
		SaveToDisk();
	}

	/// <summary>Marks the Ultimate Abilities news entry as read. Idempotent.</summary>
	public static void MarkUltimateEntrySeen()
	{
		if (_data.HasSeenUltimateEntry) return;
		_data.HasSeenUltimateEntry = true;
		SaveToDisk();
	}

	/// <summary>
	/// Unlocks the Runes news board entry.
	/// Called when the player defeats the Queen of the Frozen Wastes for the first time.
	/// Idempotent.
	/// </summary>
	public static void UnlockRuneEntry()
	{
		if (_data.HasUnlockedRuneEntry) return;
		_data.HasUnlockedRuneEntry = true;
		SaveToDisk();
	}

	/// <summary>Marks the Runes news entry as read. Idempotent.</summary>
	public static void MarkRuneEntrySeen()
	{
		if (_data.HasSeenRuneEntry) return;
		_data.HasSeenRuneEntry = true;
		SaveToDisk();
	}

	// ── In-combat tutorial overlay API ────────────────────────────────────────

	/// <summary>Marks the Deflect tutorial overlay as seen and saves to disk. Idempotent.</summary>
	public static void MarkDeflectTutorialSeen()
	{
		if (_data.HasSeenDeflectTutorial) return;
		_data.HasSeenDeflectTutorial = true;
		SaveToDisk();
	}

	/// <summary>Marks the Dispel tutorial overlay as seen and saves to disk. Idempotent.</summary>
	public static void MarkDispelTutorialSeen()
	{
		if (_data.HasSeenDispelTutorial) return;
		_data.HasSeenDispelTutorial = true;
		SaveToDisk();
	}

	/// <summary>Marks the Detonation Zone tutorial overlay as seen and saves to disk. Idempotent.</summary>
	public static void MarkDetonationTutorialSeen()
	{
		if (_data.HasSeenDetonationTutorial) return;
		_data.HasSeenDetonationTutorial = true;
		SaveToDisk();
	}

	/// <summary>Marks the targeting tutorial overlay as seen and saves to disk. Idempotent.</summary>
	public static void MarkTargetingTutorialSeen()
	{
		if (_data.HasSeenTargetingTutorial) return;
		_data.HasSeenTargetingTutorial = true;
		SaveToDisk();
	}

	/// <summary>Marks the camp merchant tutorial overlay as seen and saves to disk. Idempotent.</summary>
	public static void MarkCampMerchantTutorialSeen()
	{
		if (_data.HasSeenCampMerchantTutorial) return;
		_data.HasSeenCampMerchantTutorial = true;
		SaveToDisk();
	}

	// ── persistence ───────────────────────────────────────────────────────────

	static ProgressData LoadFromDisk()
	{
		if (!FileAccess.FileExists(FileSavePath))
			return new ProgressData();
		try
		{
			using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Read);
			return JsonSerializer.Deserialize<ProgressData>(file.GetAsText()) ?? new ProgressData();
		}
		catch
		{
			return new ProgressData();
		}
	}

	static void SaveToDisk()
	{
		using var file = FileAccess.Open(FileSavePath, FileAccess.ModeFlags.Write);
		file.StoreLine(JsonSerializer.Serialize(_data));
	}

	/// <summary>
	/// Deletes the player progress save file from disk and resets all
	/// in-memory state to default values.
	/// </summary>
	public static void DeleteSaveFile()
	{
		if (FileAccess.FileExists(FileSavePath))
			DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(FileSavePath));
		_data = new ProgressData();
	}
}
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
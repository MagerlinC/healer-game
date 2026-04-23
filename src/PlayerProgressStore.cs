using System.Text.Json;
using Godot;

namespace healerfantasy;

/// <summary>
/// Persistent store for the player's character progression across runs:
/// character level, accumulated experience, and talent points earned.
///
/// Saved to <c>user://player-progress.save</c> and survives game restarts.
///
/// XP to level up scales linearly: <see cref="XpToNextLevel"/> for the current level.
/// Each level-up grants:
///   • +1 talent point (caps the number of talents selectable per run in the Overworld)
///   • +<see cref="MaxHealthBonusPerLevel"/> max health to all friendly characters
/// </summary>
public static class PlayerProgressStore
{
	// ── constants ─────────────────────────────────────────────────────────────

	const string FileSavePath = "user://player-progress.save";

	/// <summary>Maximum character level the player can reach.</summary>
	public const int MaxLevel = 30;

	/// <summary>Base XP used in the scaling formula. XP to advance from level N is BaseXpPerLevel × N.</summary>
	public const int BaseXpPerLevel = 100;

	/// <summary>Additional max health granted to all friendly characters per level gained.</summary>
	public const float MaxHealthBonusPerLevel = 0f;

	/// <summary>XP required to advance from <paramref name="level"/> to the next level.</summary>
	public static int XpToNextLevel(int level) => BaseXpPerLevel * level;

	// ── data ──────────────────────────────────────────────────────────────────

	public sealed class ProgressData
	{
		/// <summary>Current character level (starts at 1).</summary>
		public int Level { get; set; } = 1;

		/// <summary>XP progress toward the next level (0 – XpToNextLevel(Level)-1).</summary>
		public int CurrentXp { get; set; } = 0;

		/// <summary>
		/// Total talent points available to spend in the Overworld per run.
		/// Increments by 1 on each level-up. Starts at 0 (level 1 has no points).
		/// </summary>
		public int TalentPoints { get; set; } = 0;
	}

	// ── in-memory state ───────────────────────────────────────────────────────

	static ProgressData _data = LoadFromDisk();

	/// <summary>Current character level (1-based).</summary>
	public static int Level => _data.Level;

	/// <summary>XP accumulated toward the next level.</summary>
	public static int CurrentXp => _data.CurrentXp;

	/// <summary>
	/// Total talent points the player may select in the Overworld each run.
	/// Earned one per level-up.
	/// </summary>
	public static int TalentPoints => _data.TalentPoints;

	/// <summary>
	/// Cumulative max health bonus applied to all friendly characters,
	/// equal to (Level - 1) × <see cref="MaxHealthBonusPerLevel"/>.
	/// </summary>
	public static float MaxHealthBonus => (_data.Level - 1) * MaxHealthBonusPerLevel;

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Awards <paramref name="xp"/> experience, processing any resulting level-ups.
	/// Saves immediately after any change. Does nothing if already at <see cref="MaxLevel"/>.
	/// </summary>
	/// <returns>The number of levels gained (0 if none).</returns>
	public static int AddXp(int xp)
	{
		if (_data.Level >= MaxLevel) return 0;

		var levelsGained = 0;
		_data.CurrentXp += xp;

		while (_data.Level < MaxLevel && _data.CurrentXp >= XpToNextLevel(_data.Level))
		{
			_data.CurrentXp -= XpToNextLevel(_data.Level);
			_data.Level++;
			_data.TalentPoints++;
			levelsGained++;
		}

		// At max level there's no next level to progress toward — discard leftover XP.
		if (_data.Level >= MaxLevel)
			_data.CurrentXp = 0;

		SaveToDisk();
		return levelsGained;
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
}
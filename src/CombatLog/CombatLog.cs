using System.Collections.Generic;

namespace healerfantasy.CombatLog;

/// <summary>
/// Thread-unsafe but game-safe rolling event store.
///
/// Events older than <see cref="MaxAge"/> seconds are pruned on every write.
/// Consumers call <see cref="GetRatePerSource"/> for HPS / DPS values and
/// <see cref="GetBreakdown"/> for the per-ability detail shown in tooltips.
/// </summary>
public static class CombatLog
{
	/// <summary>Rolling window used for rate calculations (seconds).</summary>
	public const double DefaultWindow = 15.0;

	/// <summary>Events older than this are discarded to bound memory usage.</summary>
	const double MaxAge = 60.0;

	static readonly List<CombatEventRecord> Events = new();

	// ── write ────────────────────────────────────────────────────────────────

	public static void Record(CombatEventRecord record)
	{
		Events.Add(record);
		PruneOld(record.Timestamp);
	}

	static void PruneOld(double currentTime)
	{
		var cutoff = currentTime - MaxAge;
		Events.RemoveAll(e => e.Timestamp < cutoff);
	}

	// ── read ─────────────────────────────────────────────────────────────────

	/// <summary>
	/// Returns the healing or damage per second for each unique source in the
	/// rolling window, keyed by <see cref="CombatEventRecord.SourceName"/>.
	/// </summary>
	public static Dictionary<string, float> GetRatePerSource(
		CombatEventType type,
		double          currentTime,
		double          window = DefaultWindow)
	{
		var cutoff = currentTime - window;
		var totals = new Dictionary<string, float>();

		foreach (var e in Events)
		{
			if (e.Type != type || e.Timestamp < cutoff) continue;
			totals.TryGetValue(e.SourceName, out var cur);
			totals[e.SourceName] = cur + e.Amount;
		}

		// Normalise to per-second rate.
		var result = new Dictionary<string, float>(totals.Count);
		foreach (var (key, total) in totals)
			result[key] = total / (float)window;

		return result;
	}

	/// <summary>
	/// Returns total amount per ability for a given source in the rolling
	/// window, keyed by <see cref="CombatEventRecord.AbilityName"/>.
	/// Used by the combat-meter tooltip to show per-ability breakdowns.
	/// </summary>
	public static Dictionary<string, float> GetBreakdown(
		string          sourceName,
		CombatEventType type,
		double          currentTime,
		double          window = DefaultWindow)
	{
		var cutoff = currentTime - window;
		var result = new Dictionary<string, float>();

		foreach (var e in Events)
		{
			if (e.Type != type || e.SourceName != sourceName || e.Timestamp < cutoff) continue;
			result.TryGetValue(e.AbilityName, out var cur);
			result[e.AbilityName] = cur + e.Amount;
		}

		return result;
	}
}

using System;
using Godot;

namespace healerfantasy.SpellSystem;

/// <summary>
/// Tracks whether a parryable attack is currently being telegraphed and
/// whether the player has successfully deflected it.
///
/// Usage flow
/// ──────────
/// 1. Boss calls <see cref="OpenWindow"/> when it begins a telegraphed wind-up.
///    This fires <see cref="WindupStarted"/> so the DeflectOverlay and any other
///    subscribers are notified — exclusively for parryable casts.
/// 2. Player casts Deflect, which calls <see cref="TryDeflect"/>.
/// 3. When the wind-up timer expires the boss calls <see cref="ConsumeResult"/>
///    to learn the outcome — true = deflected, false = hit lands.
///    This fires <see cref="WindupEnded"/> to dismiss the overlay.
/// </summary>
public static class ParryWindowManager
{
	/// <summary>True while a parryable attack wind-up is active.</summary>
	public static bool IsOpen { get; private set; }

	static bool _wasDeflected;

	// ── central parry-window events ───────────────────────────────────────────

	/// <summary>
	/// Fired when a parryable wind-up begins.
	/// Parameters: spell name, spell icon, wind-up duration in seconds.
	/// Subscribe here instead of individual boss signals so non-parryable
	/// casts (e.g. Volatile Icicle) never trigger deflect cues.
	/// </summary>
	public static event Action<string, Texture2D, float>? WindupStarted;

	/// <summary>
	/// Fired when a parryable wind-up resolves — whether deflected, landed,
	/// or cancelled by a phase transition. Always paired with a prior
	/// <see cref="WindupStarted"/>.
	/// </summary>
	public static event Action? WindupEnded;

	// ── window management ─────────────────────────────────────────────────────

	/// <summary>
	/// Opens a new parry window and fires <see cref="WindupStarted"/>.
	/// Resets any leftover deflect state from a prior window.
	/// </summary>
	public static void OpenWindow(string spellName, Texture2D icon, float duration)
	{
		IsOpen = true;
		_wasDeflected = false;
		WindupStarted?.Invoke(spellName, icon, duration);
	}

	/// <summary>
	/// Attempt to deflect the currently active parryable attack.
	/// Returns <c>true</c> and marks the window as deflected when a wind-up
	/// is in progress; returns <c>false</c> if no window is open.
	/// </summary>
	public static bool TryDeflect()
	{
		if (!IsOpen) return false;
		_wasDeflected = true;
		IsOpen = false; // close immediately — you can only deflect once
		return true;
	}

	/// <summary>
	/// Closes the window, fires <see cref="WindupEnded"/>, and returns whether
	/// the attack was deflected. Called by the boss when the wind-up timer
	/// expires (or when the cast is cancelled) to resolve the attack.
	/// </summary>
	public static bool ConsumeResult()
	{
		IsOpen = false;
		var result = _wasDeflected;
		_wasDeflected = false;
		WindupEnded?.Invoke();
		return result;
	}

	/// <summary>
	/// Resets all parry state. Call alongside <see cref="GlobalAutoLoad.Reset"/>
	/// on scene transitions so a fight that ended mid-windup doesn't leave
	/// <see cref="IsOpen"/> set for the next fight.
	/// Note: <see cref="WindupStarted"/> and <see cref="WindupEnded"/> are
	/// intentionally NOT cleared here — persistent UI nodes (e.g. DeflectOverlay)
	/// subscribe once in _Ready and must remain connected across scene reloads.
	/// </summary>
	public static void Reset()
	{
		IsOpen        = false;
		_wasDeflected = false;
	}
}

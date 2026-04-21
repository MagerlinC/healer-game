namespace healerfantasy.SpellSystem;

/// <summary>
/// Tracks whether a parryable attack is currently being telegraphed and
/// whether the player has successfully deflected it.
///
/// Usage flow
/// ──────────
/// 1. Boss calls <see cref="OpenWindow"/> when it begins a telegraphed wind-up.
/// 2. Player casts Deflect, which calls <see cref="TryDeflect"/>.
/// 3. When the wind-up timer expires the boss calls <see cref="ConsumeResult"/>
///    to learn the outcome — true = deflected, false = hit lands.
/// </summary>
public static class ParryWindowManager
{
	/// <summary>True while a parryable attack wind-up is active.</summary>
	public static bool IsOpen { get; private set; }

	static bool _wasDeflected;

	/// <summary>
	/// Opens a new parry window.
	/// Resets any leftover deflect state from a prior window.
	/// </summary>
	public static void OpenWindow()
	{
		IsOpen = true;
		_wasDeflected = false;
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
	/// Closes the window and returns whether the attack was deflected.
	/// Called by the boss when the wind-up timer expires to resolve the attack.
	/// </summary>
	public static bool ConsumeResult()
	{
		IsOpen = false;
		var result = _wasDeflected;
		_wasDeflected = false;
		return result;
	}
}

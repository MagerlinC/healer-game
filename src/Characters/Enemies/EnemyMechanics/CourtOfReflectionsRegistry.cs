namespace healerfantasy;

/// <summary>
/// Lightweight static registry that tracks which <see cref="CountessClone"/>
/// (or the real boss stand-in) the player is currently hovering over in world space.
///
/// Because the Court of Reflections mechanic hides the boss health bar and places
/// interactive sprites directly in the arena, spell targeting cannot rely on the
/// normal UI hover system. Each <see cref="CountessClone"/> updates this registry
/// every frame via <see cref="SetHovered"/>; <see cref="GameUI.GetHoveredCharacter"/>
/// queries it as a priority override so Dispel targets the hovered clone correctly.
///
/// Call <see cref="Clear"/> when the mechanic ends to ensure no stale reference
/// lingers after clones are freed.
/// </summary>
public static class CourtOfReflectionsRegistry
{
	static Character _hoveredTarget;

	/// <summary>
	/// The clone or boss the player's cursor is currently over, or null when none.
	/// Read by <see cref="UI.GameUI.GetHoveredCharacter"/> to inject world-space
	/// targets into the normal spell-targeting pipeline.
	/// </summary>
	public static Character HoveredTarget => _hoveredTarget;

	/// <summary>
	/// Called each frame by a <see cref="CountessClone"/> to register or clear its
	/// hover state. Only one clone can be hovered at a time — if <paramref name="hovered"/>
	/// is true, this source becomes the active target. If false and this source was
	/// the previous active target, the target is cleared.
	/// </summary>
	public static void SetHovered(Character source, bool hovered)
	{
		if (hovered)
			_hoveredTarget = source;
		else if (_hoveredTarget == source)
			_hoveredTarget = null;
	}

	/// <summary>
	/// Clears the registry. Call when the Court of Reflections mechanic ends and
	/// all clones are freed, to prevent stale references.
	/// </summary>
	public static void Clear()
	{
		_hoveredTarget = null;
	}
}

#nullable enable
using Godot;
using healerfantasy;

/// <summary>
/// Abstract base for any UI frame bound to a single <see cref="Character"/>.
///
/// Provides three things every frame needs:
/// <list type="bullet">
///   <item><see cref="BoundCharacter"/> — the game object used for hover-targeting.</item>
///   <item><see cref="EffectBar"/> — a <see cref="GridContainer"/> that displays
///     <see cref="EffectIndicator"/> badges. Subclasses place it in their own layout
///     (e.g. above or below the health panel) and the base class handles the
///     <see cref="Character.EffectApplied"/> / <see cref="Character.EffectRemoved"/>
///     signal wiring automatically.</item>
///   <item><see cref="IsHovered"/> — virtual hover-check used by the UI targeting
///     system. Default implementation checks <c>GetGlobalRect()</c>; subclasses may
///     restrict it to a specific inner panel.</item>
/// </list>
///
/// Inherits <see cref="VBoxContainer"/> so each concrete frame is a self-sizing
/// vertical stack — effects row plus health area — that can be placed directly in
/// any container without extra wrapper nodes.
/// </summary>
public abstract partial class CharacterFrame : VBoxContainer
{
	/// <summary>The character name this frame tracks; used to filter global signals.</summary>
	protected abstract string FrameCharacterName { get; }

	/// <summary>The game-world character bound to this frame (set via <see cref="BindCharacter"/>).</summary>
	public Character? BoundCharacter { get; private set; }

	/// <summary>
	/// Grid of <see cref="EffectIndicator"/> badges. Wraps to multiple rows as
	/// effects accumulate, growing upward while keeping the frame width fixed.
	/// New effects are inserted at index 0 so the most recently applied badge
	/// always appears in the top-left position.
	/// </summary>
	protected readonly GridContainer EffectBar;

	/// <summary>
	/// Number of columns in the effect grid. Defaults to 5, which fills a
	/// 160 px frame at the standard 28 px indicator size with 3 px gaps.
	/// Override in subclasses that use a different frame width.
	/// </summary>
	protected virtual int EffectGridColumns => 5;

	public int _effectIndicatorSize = 28;
	protected CharacterFrame()
	{
		EffectBar = new GridContainer();
		EffectBar.AddThemeConstantOverride("h_separation", 3);
		EffectBar.AddThemeConstantOverride("v_separation", 3);
		EffectBar.MouseFilter = MouseFilterEnum.Ignore;
	}

	/// <summary>
	/// Subscribes to the global <see cref="Character.EffectApplied"/> and
	/// <see cref="Character.EffectRemoved"/> signals, filtered to
	/// <see cref="FrameCharacterName"/>. Subclasses must call <c>base._Ready()</c>
	/// (after adding <see cref="EffectBar"/> to the tree).
	/// </summary>
	public override void _Ready()
	{
		// Set columns here (after construction) so subclass virtual overrides resolve correctly.
		EffectBar.Columns = EffectGridColumns;

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.EffectApplied),
			Callable.From((string name, CharacterEffect effect) =>
			{
				if (name == FrameCharacterName) ShowEffectIndicator(effect);
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.EffectRemoved),
			Callable.From((string name, string id) =>
			{
				if (name == FrameCharacterName) HideEffectIndicator(id);
			}));
	}

	/// <summary>Associate this frame with the given character for spell targeting.</summary>
	public void BindCharacter(Character character)
	{
		BoundCharacter = character;
	}

	/// <summary>
	/// Returns <c>true</c> when the cursor is over this frame's interactive area.
	/// Default: the frame's own global rect. Override to restrict to an inner panel.
	/// </summary>
	public virtual bool IsHovered()
	{
		var mousePos = GetViewport().GetMousePosition();
		return GetGlobalRect().HasPoint(mousePos);
	}

	// ── private ───────────────────────────────────────────────────────────────

	void ShowEffectIndicator(CharacterEffect effect)
	{
		// Remove the stale badge so a refreshed effect doesn't appear twice.
		HideEffectIndicator(effect.EffectId);
		var indicator = new EffectIndicator(effect, _effectIndicatorSize);
		EffectBar.AddChild(indicator);
		// Move to the front so the newest effect always appears top-left in the grid.
		EffectBar.MoveChild(indicator, 0);
	}

	void HideEffectIndicator(string effectId)
	{
		foreach (var child in EffectBar.GetChildren())
		{
			if (child is EffectIndicator ind && ind.CharacterEffect.EffectId == effectId)
			{
				ind.QueueFree();
				return;
			}
		}
	}
}
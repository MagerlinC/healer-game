#nullable enable
using Godot;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Shared cursor-following tooltip singleton.
///
/// WHY we can't use Godot's built-in TooltipText:
///   Godot positions engine-managed tooltip popups in root-viewport space.
///   Controls inside a CanvasLayer live in a different coordinate space, so
///   the native tooltip either never appears or appears at the wrong position.
///   This singleton owns a single high-layer CanvasLayer panel (layer 50) that
///   is repositioned every frame to follow the cursor — the same technique used
///   by EffectIndicator, now centralised so every UI element can share it.
///
/// Usage:
///   • Any control: call <see cref="Show"/> on MouseEntered, <see cref="Hide"/>
///     on MouseExited. For live-updating content (e.g. countdowns) call
///     <see cref="Show"/> again each frame while hovered.
///   • Call <see cref="FormatSpellTooltip"/> for consistent spell card text.
///
/// Wiring: add a <see cref="GameTooltip"/> node as an early child of the World
/// scene from <c>World._Ready</c> so <see cref="Instance"/> is set before any
/// other UI node tries to use it.
/// </summary>
public partial class GameTooltip : CanvasLayer
{
	public static GameTooltip? Instance { get; private set; }

	// ── style ─────────────────────────────────────────────────────────────────
	static readonly Color TooltipBg     = new(0.08f, 0.07f, 0.06f, 0.96f);
	static readonly Color TooltipBorder = new(0.55f, 0.45f, 0.25f, 0.90f);
	static readonly Color TooltipText   = new(0.95f, 0.88f, 0.70f);

	// ── private state ─────────────────────────────────────────────────────────
	PanelContainer _panel = null!;
	Label          _label = null!;
	bool           _isShowing;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Instance = this;
		Layer = 50;                              // above all game/UI layers
		ProcessMode = ProcessModeEnum.Always;    // works while game is paused

		var style = new StyleBoxFlat();
		style.BgColor = TooltipBg;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor        = TooltipBorder;
		style.ContentMarginLeft  = style.ContentMarginRight  = 10f;
		style.ContentMarginTop   = style.ContentMarginBottom = 6f;

		_panel = new PanelContainer();
		_panel.AddThemeStyleboxOverride("panel", style);
		_panel.MouseFilter = Control.MouseFilterEnum.Ignore;
		_panel.Visible     = false;
		AddChild(_panel);

		_label = new Label();
		_label.AutowrapMode = TextServer.AutowrapMode.Off;
		_label.MouseFilter  = Control.MouseFilterEnum.Ignore;
		_label.AddThemeFontSizeOverride("font_size", 12);
		_label.AddThemeColorOverride("font_color", TooltipText);
		_panel.AddChild(_label);
	}

	public override void _Process(double delta)
	{
		if (_isShowing)
			Reposition();
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Show the tooltip with <paramref name="text"/> following the cursor.
	/// Safe to call every frame while hovered — text and position update in place.
	/// </summary>
	public static void Show(string text)
	{
		if (Instance is null) return;
		Instance._label.Text   = text;
		Instance._panel.Visible = true;
		Instance._isShowing    = true;
		Instance.Reposition();
	}

	/// <summary>Hide the tooltip.</summary>
	public static void Hide()
	{
		if (Instance is null) return;
		Instance._panel.Visible = false;
		Instance._isShowing    = false;
	}

	/// <summary>
	/// Formats a consistent multi-line tooltip for a spell card, used by both
	/// the ActionBar and the SpellbookSelector.
	/// </summary>
	public static string FormatSpellTooltip(SpellResource spell)
	{
		var castInfo = spell.CastTime <= 0f
			? "Instant"
			: $"{spell.CastTime:F1}s cast";

		return $"{spell.Name}\n{spell.Description}\nMana: {(int)spell.ManaCost}  •  {castInfo}";
	}

	// ── private ───────────────────────────────────────────────────────────────
	void Reposition()
	{
		var mouse  = GetViewport().GetMousePosition();
		var pos    = mouse + new Vector2(14f, 14f);
		var vpSize = GetViewport().GetVisibleRect().Size;
		var pSize  = _panel.Size;

		// Nudge left/up if the panel would overflow the viewport edge.
		if (pos.X + pSize.X > vpSize.X) pos.X = mouse.X - pSize.X - 6f;
		if (pos.Y + pSize.Y > vpSize.Y) pos.Y = mouse.Y - pSize.Y - 6f;

		_panel.Position = pos;
	}
}

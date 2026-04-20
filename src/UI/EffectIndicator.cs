using System.Text.RegularExpressions;
using Godot;
using healerfantasy;

/// <summary>
/// A small icon badge displayed on a party frame while an effect is active.
/// Shows the effect's icon with a ceiling-integer countdown overlaid at the bottom.
/// Self-destructs when its timer reaches zero.
///
/// Hovering the badge shows a custom tooltip with the effect's display name and
/// live remaining duration.
///
/// WHY a custom tooltip rather than TooltipText:
///   Godot's built-in tooltip system positions the popup in root-viewport space.
///   Controls inside a CanvasLayer live in a different coordinate space, so the
///   engine-managed tooltip popup never appears at the right position (or at all).
///   Instead we own a <see cref="CanvasLayer"/> child (layer 50, above the game
///   UI at layer 10) that holds the tooltip panel and is repositioned every frame
///   to follow the cursor.
/// </summary>
public partial class EffectIndicator : PanelContainer
{
	// ── public ───────────────────────────────────────────────────────────────
	public CharacterEffect CharacterEffect { get; private set; }

	// ── private ───────────────────────────────────────────────────────────────
	string _displayName;
	Label _countLabel;

	// Custom tooltip nodes (created in _Ready, destroyed with this node)
	PanelContainer _tooltipPanel;
	Label _tooltipLabel;
	bool _hovered;

	const int TooltipLayer = 50; // above GameUI (10) and TalentSelector (15)

	static readonly Color BadgeBorder = new(0.25f, 0.70f, 0.35f, 0.90f);
	static readonly Color TooltipBg = new(0.08f, 0.07f, 0.06f, 0.96f);
	static readonly Color TooltipBorder = new(0.55f, 0.45f, 0.25f, 0.90f);
	static readonly Color TooltipTitle = new(0.95f, 0.88f, 0.70f);
	static readonly Color TooltipBody = new(0.70f, 0.65f, 0.58f);

	// ── constructor ──────────────────────────────────────────────────────────
	public EffectIndicator(CharacterEffect effect)
	{
		CharacterEffect = effect;
		_displayName = FormatDisplayName(effect.EffectId);

		CustomMinimumSize = new Vector2(24, 24);
		MouseFilter = MouseFilterEnum.Stop; // must be non-Ignore to receive mouse events

		// ── badge style ──────────────────────────────────────────────────────
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.10f, 0.10f, 0.10f, 0.85f);
		style.SetCornerRadiusAll(3);
		style.SetBorderWidthAll(1);
		style.BorderColor = BadgeBorder;
		style.ContentMarginLeft = 1f;
		style.ContentMarginRight = 1f;
		style.ContentMarginTop = 1f;
		style.ContentMarginBottom = 1f;
		AddThemeStyleboxOverride("panel", style);

		// Stacking layer for icon + label
		var inner = new Control();
		inner.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(inner);

		// Effect icon fills the badge
		if (effect.Icon != null)
		{
			var iconRect = new TextureRect();
			iconRect.Texture = effect.Icon;
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = MouseFilterEnum.Ignore;
			iconRect.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
			inner.AddChild(iconRect);
		}

		// Countdown label — bottom-right corner, small + shadowed for legibility
		_countLabel = new Label();
		_countLabel.MouseFilter = MouseFilterEnum.Ignore;
		_countLabel.AddThemeFontSizeOverride("font_size", 9);
		_countLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 1f));
		_countLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 1f));
		_countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		_countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		_countLabel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
		_countLabel.GrowHorizontal = GrowDirection.Begin;
		_countLabel.GrowVertical = GrowDirection.Begin;
		inner.AddChild(_countLabel);

		UpdateCountLabel();

		// ── mouse hover wiring ───────────────────────────────────────────────
		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
	}

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// Build the tooltip inside a high-layer CanvasLayer so it renders
		// above all game elements regardless of draw order.
		var layer = new CanvasLayer();
		layer.Layer = TooltipLayer;
		AddChild(layer);

		_tooltipPanel = BuildTooltipPanel(out _tooltipLabel);
		_tooltipPanel.Visible = false;
		layer.AddChild(_tooltipPanel);
	}

	public override void _Process(double delta)
	{
		UpdateCountLabel();

		if (_hovered && _tooltipPanel != null)
			UpdateTooltipPosition();
	}

	// ── private helpers ──────────────────────────────────────────────────────
	void OnMouseEntered()
	{
		_hovered = true;
		if (_tooltipPanel == null) return;
		RefreshTooltipText();
		UpdateTooltipPosition();
		_tooltipPanel.Visible = true;
	}

	void OnMouseExited()
	{
		_hovered = false;
		if (_tooltipPanel != null)
			_tooltipPanel.Visible = false;
	}

	void UpdateCountLabel()
	{
		_countLabel.Text = Mathf.CeilToInt(CharacterEffect.Remaining).ToString();
	}

	void RefreshTooltipText()
	{
		_tooltipLabel.Text = $"{_displayName}\n{Mathf.CeilToInt(CharacterEffect.Remaining)}s remaining";
	}

	void UpdateTooltipPosition()
	{
		// GetViewport().GetMousePosition() returns screen-space pixel coordinates,
		// which match the coordinate space of a default CanvasLayer child.
		var mouse = GetViewport().GetMousePosition();
		var offset = new Vector2(14f, 14f);
		var pos = mouse + offset;

		// Keep the panel on screen — nudge left/up if it would overflow.
		var vpSize = GetViewport().GetVisibleRect().Size;
		var pSize = _tooltipPanel.Size;
		if (pos.X + pSize.X > vpSize.X) pos.X = mouse.X - pSize.X - 6f;
		if (pos.Y + pSize.Y > vpSize.Y) pos.Y = mouse.Y - pSize.Y - 6f;

		_tooltipPanel.Position = pos;

		// Also update text every frame so the countdown stays live while hovered.
		RefreshTooltipText();
	}

	static PanelContainer BuildTooltipPanel(out Label label)
	{
		var panel = new PanelContainer();

		var style = new StyleBoxFlat();
		style.BgColor = TooltipBg;
		style.SetCornerRadiusAll(5);
		style.SetBorderWidthAll(1);
		style.BorderColor = TooltipBorder;
		style.ContentMarginLeft = 10f;
		style.ContentMarginRight = 10f;
		style.ContentMarginTop = 6f;
		style.ContentMarginBottom = 6f;
		panel.AddThemeStyleboxOverride("panel", style);
		panel.MouseFilter = MouseFilterEnum.Ignore;

		label = new Label();
		label.AutowrapMode = TextServer.AutowrapMode.Off;
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.AddThemeFontSizeOverride("font_size", 12);
		// First line (name) is styled warm-gold; second line (duration) is dimmer.
		// We use a single Label with a RichTextLabel-style note below — for simplicity
		// both lines share the same colour here; split to RichTextLabel if you want
		// per-line styling later.
		label.AddThemeColorOverride("font_color", TooltipTitle);
		panel.AddChild(label);

		return panel;
	}

	/// <summary>
	/// Converts a PascalCase effect ID into a space-separated display name.
	/// "ShieldingReinvigoration" → "Shielding Reinvigoration"
	/// "ArcaneMastery"           → "Arcane Mastery"
	/// "HealOverTimeEffect"      → "Heal Over Time Effect"
	/// </summary>
	static string FormatDisplayName(string id)
	{
		return Regex.Replace(id, @"(?<=[a-z])(?=[A-Z])", " ");
	}
}
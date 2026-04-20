using Godot;
using healerfantasy;

namespace healerfantasy.UI;

/// <summary>
/// Boss health bar: a wide red progress bar displayed across the top of the screen.
///
/// Inherits effect-badge management and character binding from
/// <see cref="CharacterFrame"/>. Effect indicators appear in a row directly
/// below the health bar when the boss has active effects (e.g. Decay).
///
/// The bar stays hidden until the first <see cref="Character.HealthChanged"/>
/// signal arrives for the boss, so it doesn't render on the title screen or
/// before combat begins.
///
/// Hover detection is restricted to the inner health panel so the effects row
/// does not accidentally widen the targeting area.
/// </summary>
public partial class BossHealthBar : CharacterFrame
{
	// ── constants ─────────────────────────────────────────────────────────────
	static readonly Color BorderDefault = new(0.32f, 0.26f, 0.26f);
	static readonly Color BorderHovered = new(0.90f, 0.80f, 0.20f);
	static readonly Color BarTextColor  = new(0.95f, 0.90f, 0.85f);

	protected override string FrameCharacterName => GameConstants.Boss1Name;

	// ── node refs ─────────────────────────────────────────────────────────────
	PanelContainer _innerPanel         = null!;
	Label          _nameLabel          = null!;
	Label          _currentHealthLabel = null!;
	ProgressBar    _bar                = null!;

	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Visible = false; // hidden until the boss first emits HealthChanged
		AddThemeConstantOverride("separation", 4);

		// ── health panel (top slot in the VBox) ───────────────────────────────
		BuildHealthPanel();

		// ── effect-badge row (below the health bar) ───────────────────────────
		// EffectBar is created by CharacterFrame; we just place it here.
		AddChild(EffectBar);

		// ── subscribe health updates ──────────────────────────────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.HealthChanged),
			Callable.From((string charName, float current, float max) =>
			{
				if (charName == GameConstants.Boss1Name) UpdateProgress(current, max);
			}));

		base._Ready(); // subscribe effect-badge signals
	}

	/// <summary>
	/// Hover check targets only the health panel, not the effect-badge row
	/// below it, to keep targeting precise.
	/// </summary>
	public override bool IsHovered()
	{
		var mousePos = GetViewport().GetMousePosition();
		return _innerPanel?.GetGlobalRect().HasPoint(mousePos) ?? false;
	}

	// ── private ───────────────────────────────────────────────────────────────

	void UpdateProgress(float current, float max)
	{
		_bar.Value                  = Mathf.Clamp(current / max, 0f, 1f);
		_nameLabel.Text             = GameConstants.Boss1Name;
		_currentHealthLabel.Text    = $"{current:F0} / {max:F0}";
		Visible = true;
	}

	void BuildHealthPanel()
	{
		// Dark outer panel — matches the same dark-red palette as the bar fill.
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.15f, 0.05f, 0.05f, 0.8f);
		panelStyle.SetCornerRadiusAll(5);
		panelStyle.SetBorderWidthAll(1);
		panelStyle.BorderColor        = BorderDefault;
		panelStyle.ContentMarginLeft  = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop   = 6f;
		panelStyle.ContentMarginBottom = 6f;

		_innerPanel = new PanelContainer();
		_innerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_innerPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_innerPanel.MouseEntered += () => panelStyle.BorderColor = BorderHovered;
		_innerPanel.MouseExited  += () => panelStyle.BorderColor = BorderDefault;

		AddChild(_innerPanel);

		// ── main row ──────────────────────────────────────────────────────────
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		_innerPanel.AddChild(row);

		// ── overlay container (bar + overlaid text) ───────────────────────────
		var overlay = new Control();
		overlay.MouseFilter         = MouseFilterEnum.Ignore;
		overlay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		overlay.CustomMinimumSize   = new Vector2(0f, 24f);
		row.AddChild(overlay);

		// ── progress bar (fills the entire overlay) ───────────────────────────
		_bar = new ProgressBar();
		_bar.AnchorLeft = 0; _bar.AnchorRight  = 1;
		_bar.AnchorTop  = 0; _bar.AnchorBottom = 1;
		_bar.OffsetLeft = _bar.OffsetRight = _bar.OffsetTop = _bar.OffsetBottom = 0;
		_bar.ShowPercentage = false;
		_bar.MinValue       = 0f;
		_bar.MaxValue       = 1f;
		_bar.Value          = 0f;
		_bar.MouseFilter    = MouseFilterEnum.Ignore;

		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0.25f, 0.08f, 0.08f, 0.9f);
		barBg.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);
		barFill.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("fill", barFill);

		overlay.AddChild(_bar);

		// ── name label (left-aligned, over the bar) ───────────────────────────
		_nameLabel = new Label();
		_nameLabel.AnchorLeft   = 0; _nameLabel.AnchorRight  = 1;
		_nameLabel.AnchorTop    = 0; _nameLabel.AnchorBottom = 1;
		_nameLabel.OffsetLeft   = 8; _nameLabel.OffsetRight  = -40;
		_nameLabel.VerticalAlignment = VerticalAlignment.Center;
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", BarTextColor);
		_nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		overlay.AddChild(_nameLabel);

		// ── health label (right-aligned, over the bar) ────────────────────────
		_currentHealthLabel = new Label();
		_currentHealthLabel.AnchorLeft   = 1; _currentHealthLabel.AnchorRight  = 1;
		_currentHealthLabel.AnchorTop    = 0; _currentHealthLabel.AnchorBottom = 1;
		_currentHealthLabel.OffsetLeft   = -100; _currentHealthLabel.OffsetRight = -8;
		_currentHealthLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_currentHealthLabel.VerticalAlignment   = VerticalAlignment.Center;
		_currentHealthLabel.AddThemeFontSizeOverride("font_size", 13);
		_currentHealthLabel.AddThemeColorOverride("font_color", BarTextColor);
		_currentHealthLabel.MouseFilter = MouseFilterEnum.Ignore;
		overlay.AddChild(_currentHealthLabel);
	}
}

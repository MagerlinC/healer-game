#nullable enable
using Godot;
using healerfantasy;

/// <summary>
/// A single party-member health frame: an effect-badge row above a dark health panel.
///
/// Inherits effect-badge management and character binding from
/// <see cref="CharacterFrame"/>.  Adds health-bar, shield-bar, and name-label
/// rendering by subscribing to <see cref="Character.HealthChanged"/> and
/// <see cref="Character.ShieldChanged"/> filtered to its own character name.
///
/// Hover detection is restricted to the inner health panel so the effects row
/// above doesn't accidentally swallow targeting clicks.
/// </summary>
public partial class PartyFrame : CharacterFrame
{
	// ── constants ─────────────────────────────────────────────────────────────
	static readonly Color BorderDefault = new(0.32f, 0.26f, 0.26f);
	static readonly Color BorderHovered = new(0.90f, 0.80f, 0.20f);
	static readonly Color FrameTextColor = new(0.90f, 0.87f, 0.83f);


	// ── per-member config ─────────────────────────────────────────────────────
	readonly string _name;
	readonly Color _barColor;
	readonly float _maxHp;
	readonly bool _showItemEffects;

	protected override string FrameCharacterName => _name;

	// ── node refs ─────────────────────────────────────────────────────────────
	PanelContainer _panel = null!;
	ProgressBar _healthBar = null!;
	Label _currentHealthLabel = null!;
	ProgressBar _shieldBar = null!;
	StyleBoxFlat _panelStyle = null!;

	/// <param name="showItemEffects">
	/// When <c>true</c>, an <see cref="ItemEffectBar"/> is added below the
	/// health panel to display active item-proc indicators.  Enable only for
	/// the healer frame so item effects are clearly player-owned.
	/// </param>
	public PartyFrame(string name, Color barColor, float maxHp, bool showItemEffects = false)
	{
		_name = name;
		_barColor = barColor;
		_maxHp = maxHp;
		_showItemEffects = showItemEffects;
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		AddThemeConstantOverride("separation", 2);

		// ── effects row (above the health panel) ──────────────────────────────
		AddChild(EffectBar);

		// ── outer panel ───────────────────────────────────────────────────────
		_panel = new PanelContainer();
		_panel.CustomMinimumSize = new Vector2(160, 75);

		_panelStyle = new StyleBoxFlat();
		_panelStyle.BgColor = new Color(0.11f, 0.09f, 0.09f, 0.95f);
		_panelStyle.SetCornerRadiusAll(6);
		_panelStyle.SetBorderWidthAll(2);
		_panelStyle.BorderColor = BorderDefault;
		_panelStyle.ContentMarginLeft = 8f;
		_panelStyle.ContentMarginRight = 8f;
		_panelStyle.ContentMarginTop = 5f;
		_panelStyle.ContentMarginBottom = 5f;
		_panel.AddThemeStyleboxOverride("panel", _panelStyle);

		// ── health bar ────────────────────────────────────────────────────────
		_healthBar = new ProgressBar();
		_healthBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_healthBar.SizeFlagsVertical = SizeFlags.ExpandFill;
		_healthBar.ShowPercentage = false;
		_healthBar.MaxValue = _maxHp;
		_healthBar.Value = _maxHp;
		_healthBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.16f, 0.13f, 0.13f) });
		_healthBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = _barColor });
		_panel.AddChild(_healthBar);

		var textBox = new VBoxContainer();
		textBox.MouseFilter = MouseFilterEnum.Ignore;
		_panel.AddChild(textBox);

		var centeringContainer = new CenterContainer();
		centeringContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
		centeringContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		textBox.AddChild(centeringContainer);

		var innerVBox = new VBoxContainer();
		innerVBox.AddThemeConstantOverride("separation", 1);
		centeringContainer.AddChild(innerVBox);


		// ── name label ────────────────────────────────────────────────────────
		var nameLabel = GenerateTextLabel(14);
		nameLabel.Text = _name;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.VerticalAlignment = VerticalAlignment.Center;
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		innerVBox.AddChild(nameLabel);

		// HP label
		_currentHealthLabel = GenerateTextLabel(10);
		_currentHealthLabel.Text = $"{_maxHp:F0}/{_maxHp:F0}";
		_currentHealthLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_currentHealthLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_currentHealthLabel.VerticalAlignment = VerticalAlignment.Center;
		innerVBox.AddChild(_currentHealthLabel);

		// ── shield overlay ────────────────────────────────────────────────────
		_shieldBar = new ProgressBar();
		_shieldBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_shieldBar.SizeFlagsVertical = SizeFlags.ExpandFill;
		_shieldBar.ShowPercentage = false;
		_shieldBar.MaxValue = _maxHp;
		_shieldBar.Value = 0f;
		_shieldBar.MouseFilter = MouseFilterEnum.Ignore;
		_shieldBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) });
		_shieldBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color(0.45f, 0.70f, 1.00f, 0.50f) });
		_panel.AddChild(_shieldBar);

		AddChild(_panel);

		// ── item-effect row (below the health panel, healer only) ─────────────
		// The overlay is a zero-height Control so it contributes nothing to the
		// VBox layout (keeping the healer frame the same height as the others).
		// ClipContents = false lets the ItemEffectBar render outside the overlay's
		// zero bounds, hanging visually below the health panel.
		if (_showItemEffects)
		{
			var overlay = new Control();
			overlay.CustomMinimumSize = Vector2.Zero;
			overlay.SizeFlagsVertical = SizeFlags.ShrinkBegin;
			overlay.ClipContents = false;
			overlay.MouseFilter = MouseFilterEnum.Ignore;

			var itemEffectBar = new ItemEffectBar();
			// Anchor top & bottom to the overlay's bottom edge, then push the
			// bottom offset down by the bar's natural height (32 px + 3 px gap).
			itemEffectBar.AnchorTop = 1f;
			itemEffectBar.AnchorBottom = 1f;
			itemEffectBar.AnchorLeft = 0f;
			itemEffectBar.AnchorRight = 1f;
			itemEffectBar.OffsetBottom = 35f;

			overlay.AddChild(itemEffectBar);
			AddChild(overlay);
		}

		// ── hover border highlight ────────────────────────────────────────────
		_panel.MouseEntered += () => _panelStyle.BorderColor = BorderHovered;
		_panel.MouseExited += () => _panelStyle.BorderColor = BorderDefault;

		// ── health / shield signal subscriptions ──────────────────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.HealthChanged),
			Callable.From((string name, float cur, float max) =>
			{
				if (name == _name) SetHealth(cur, max);
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.ShieldChanged),
			Callable.From((string name, float shield, float maxHp) =>
			{
				if (name == _name) SetShield(shield, maxHp);
			}));

		base._Ready(); // subscribe effect-badge signals last
	}

	/// <summary>
	/// Hover check targets only the inner health panel, not the effects row above,
	/// so the player can reliably click-target a party member without the badges
	/// interfering.
	/// </summary>
	public override bool IsHovered()
	{
		var mousePos = GetViewport().GetMousePosition();
		return _panel?.GetGlobalRect().HasPoint(mousePos) ?? false;
	}

	// ── private ───────────────────────────────────────────────────────────────

	void SetHealth(float current, float max)
	{
		_healthBar.MaxValue = max;
		_healthBar.Value = current;
		_currentHealthLabel.Text = $"{current:F0}/{max:F0}";
	}

	void SetShield(float shield, float maxHp)
	{
		_shieldBar.MaxValue = maxHp;
		_shieldBar.Value = shield;
	}

	Label GenerateTextLabel(int fontSize)
	{
		var label = new Label();
		label.AddThemeColorOverride("font_color", FrameTextColor);
		label.MouseFilter = MouseFilterEnum.Ignore;
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeConstantOverride("outline_size", 2);
		label.AddThemeColorOverride("font_shadow_color", Colors.Black);
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 1);
		return label;
	}
}
#nullable enable
using System;
using Godot;
using healerfantasy;
using healerfantasy.LastLight;
using healerfantasy.LastLight.Hud;

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
	static readonly Color BorderDefault = LastLightHud.Iron;
	static readonly Color BorderHovered = LastLightHud.Light;
	static readonly Color BorderBossTargeted = LastLightHud.BloodBright;

	// ── per-member config ─────────────────────────────────────────────────────
	readonly string _name;
	readonly float _maxHp;
	readonly bool _showItemEffects;

	protected override string FrameCharacterName => _name;

	// ── node refs ─────────────────────────────────────────────────────────────
	Control _frameWrapper = null!;
	PanelContainer _panel = null!;
	ProgressBar _healthBar = null!;
	Label _currentHealthLabel = null!;
	ProgressBar _shieldBar = null!;
	ProgressBar _absorbBar = null!;
	StyleBoxFlat _panelStyle = null!;
	LightHealthFill? _lightFill;

	// ── overlay refs ──────────────────────────────────────────────────────────
	CornerBrackets _selectionFrame = null!;
	Panel _targetingRing = null!;
	StyleBoxFlat _targetingRingStyle = null!;

	// ── targeting / selection state ────────────────────────────────────────────
	bool _isBossTargeted;
	bool _isHovered;
	bool _isDefaultTarget;

	/// <summary>
	/// Invoked when the player left-clicks this frame to lock it as their
	/// default healing target.  Set by <see cref="PartyFrames"/> after construction.
	/// </summary>
	public Action<Character?>? OnDefaultTargetClicked;

	/// <param name="showItemEffects">
	/// When <c>true</c>, an <see cref="ItemEffectBar"/> is added below the
	/// health panel to display active item-proc indicators.  Enable only for
	/// the healer frame so item effects are clearly player-owned.
	/// </param>
	public PartyFrame(string name, float maxHp, bool showItemEffects = false)
	{
		_name = name;
		_maxHp = maxHp;
		_showItemEffects = showItemEffects;
	}

	const float FrameWidth = 160f;

	/// <summary>
	/// As many badges per row as fit the frame; more effects wrap onto new rows
	/// above (the frames are bottom-aligned), so the frame never widens.
	/// </summary>
	protected override int EffectGridColumns => ColumnsThatFit(FrameWidth);

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Pass;
		AddThemeConstantOverride("separation", 2);

		// ── effects row (above the health panel) ──────────────────────────────
		AddChild(EffectBar);

		// ── outer panel ───────────────────────────────────────────────────────
		_frameWrapper = new Control();
		_frameWrapper.CustomMinimumSize = new Vector2(FrameWidth, 75);
		_frameWrapper.SizeFlagsHorizontal = SizeFlags.ExpandFill;

		_panel = new PanelContainer();
		_panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

		_panelStyle = new StyleBoxFlat();
		_panelStyle.BgColor = new Color(0.11f, 0.09f, 0.09f, 0.95f);
		_panelStyle.SetCornerRadiusAll(6);
		_panelStyle.SetBorderWidthAll(2);
		_panelStyle.BgColor = LastLightHud.Pit;
		_panelStyle.SetCornerRadiusAll(0);
		_panelStyle.SetBorderWidthAll(1);
		_panelStyle.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
		_panelStyle.ShadowSize = 5;

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
		_healthBar.AddThemeStyleboxOverride("background", new StyleBoxEmpty());
		_healthBar.AddThemeStyleboxOverride("fill", new StyleBoxEmpty());
		_panel.AddChild(_healthBar);

		// Health is drawn as light meeting the dark at a jagged edge.
		_lightFill = new LightHealthFill();
		_lightFill.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_lightFill.SizeFlagsVertical = SizeFlags.ExpandFill;
		_panel.AddChild(_lightFill);

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
		_shieldBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = LastLightHud.ShieldGlass });
		_panel.AddChild(_shieldBar);

		// ── heal-absorption overlay (Rune of the Umbra) ────────────────────────
		// Dark purple bar that fills from the right edge of current health inward,
		// showing how much incoming healing will be consumed before reaching health.
		_absorbBar = new ProgressBar();
		_absorbBar.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_absorbBar.SizeFlagsVertical = SizeFlags.ExpandFill;
		_absorbBar.ShowPercentage = false;
		_absorbBar.MaxValue = _maxHp;
		_absorbBar.Value = 0f;
		_absorbBar.MouseFilter = MouseFilterEnum.Ignore;
		_absorbBar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0f, 0f, 0f, 0f) });
		_absorbBar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = LastLightHud.VoidAbsorb });
		_panel.AddChild(_absorbBar);

		// ── default-target selection: gold corner brackets around the frame ──
		_selectionFrame = new CornerBrackets();
		_selectionFrame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_selectionFrame.OffsetLeft = -4f;
		_selectionFrame.OffsetTop = -4f;
		_selectionFrame.OffsetRight = 4f;
		_selectionFrame.OffsetBottom = 4f;
		_selectionFrame.Visible = false;

		_targetingRing = new Panel();
		_targetingRing.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_targetingRing.MouseFilter = MouseFilterEnum.Ignore;
		_targetingRing.Visible = false;

		_targetingRingStyle = new StyleBoxFlat();
		_targetingRingStyle.BgColor = new Color(0f, 0f, 0f, 0f);
		_targetingRingStyle.SetCornerRadiusAll(0);
		_targetingRingStyle.SetBorderWidthAll(2);
		_targetingRingStyle.BorderColor = BorderBossTargeted;
		_targetingRing.AddThemeStyleboxOverride("panel", _targetingRingStyle);

		_frameWrapper.AddChild(_panel);
		_frameWrapper.AddChild(_selectionFrame);
		_frameWrapper.AddChild(_targetingRing);

		AddChild(_frameWrapper);

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
		_panel.MouseEntered += () =>
		{
			_isHovered = true;
			UpdateHighlight();
		};
		_panel.MouseExited += () =>
		{
			_isHovered = false;
			UpdateHighlight();
		};

		// ── default-target click ──────────────────────────────────────────────
		// Left-clicking the frame locks or unlocks it as the default heal target.
		_panel.GuiInput += evt =>
		{
			if (evt is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
				OnDefaultTargetClicked?.Invoke(BoundCharacter);
		};

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

		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.HealAbsorptionChanged),
			Callable.From((string name, float absorb, float maxHp) =>
			{
				if (name == _name) SetAbsorb(absorb, maxHp);
			}));

		base._Ready(); // subscribe effect-badge signals last
	}

	/// <summary>
	/// Lights the frame border red while a boss ability is targeting this member.
	/// Called by <see cref="PartyFrames"/> when a boss cast windup starts or ends.
	/// </summary>
	public void SetBossTargeted(bool targeted)
	{
		_isBossTargeted = targeted;
		UpdateHighlight();
	}

	/// <summary>
	/// Marks this frame as the player's locked default healing target, showing a
	/// subtle brightening overlay so it stands out without clashing with the red
	/// boss-targeting border.
	/// </summary>
	public void SetIsDefaultTarget(bool isDefault)
	{
		_isDefaultTarget = isDefault;
		_selectionFrame.Visible = isDefault;
	}

	// Applies border colour priority: boss targeting > hover > default.
	void UpdateHighlight()
	{
		_panelStyle.BorderColor = _isHovered
			? BorderHovered
			: BorderDefault;
		_targetingRing.Visible = _isBossTargeted;
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

	/// <summary>
	/// Returns the screen-space <see cref="Rect2"/> of the inner health panel.
	/// Used by <see cref="healerfantasy.UI.CombatTutorialManager"/> to compute the
	/// spotlight highlight rect for the Dispel tutorial overlay.
	/// </summary>
	public Rect2 GetPanelGlobalRect()
	{
		return _panel?.GetGlobalRect() ?? new Rect2();
	}

	// ── private ───────────────────────────────────────────────────────────────

	void SetHealth(float current, float max)
	{
		_healthBar.MaxValue = max;
		_healthBar.Value = current;
		_currentHealthLabel.Text = $"{current:F0}/{max:F0}";
		_lightFill?.SetRatio(max > 0f ? current / max : 0f);
	}

	void SetShield(float shield, float maxHp)
	{
		_shieldBar.MaxValue = maxHp;
		_shieldBar.Value = shield;
	}

	void SetAbsorb(float absorb, float maxHp)
	{
		_absorbBar.MaxValue = maxHp;
		_absorbBar.Value = absorb;
	}

	Label GenerateTextLabel(int fontSize)
	{
		var label = new Label();
		label.MouseFilter = MouseFilterEnum.Ignore;
		// Names a touch larger (heavy serif), HP numbers in bold sans.
		LastLightHud.Style(label, fontSize + 2, fontSize >= 14 ? LastLightHud.LightPale : LastLightHud.Text);
		return label;
	}
}
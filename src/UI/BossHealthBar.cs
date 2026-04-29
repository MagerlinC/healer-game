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
	static readonly Color BarTextColor = new(0.95f, 0.90f, 0.85f);

	/// <summary>
	/// Horizontal inset shared by both the health panel and the effect-badge row,
	/// so they always align regardless of viewport width.
	/// </summary>
	const int SideMargin = 48;

	// When non-null this overrides RunState to show a specific boss (e.g. second twin).
	readonly string? _bossNameOverride;

	// Evaluated dynamically so it always matches whichever boss is active this run.
	protected override string FrameCharacterName =>
		_bossNameOverride ?? RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name;

	// ── node refs ─────────────────────────────────────────────────────────────
	PanelContainer _innerPanel = null!;
	Label _nameLabel = null!;
	Label _currentHealthLabel = null!;
	ProgressBar _bar = null!;

	/// <summary>
	/// Thin vertical marker drawn on the health bar during Sanguine Siphon,
	/// showing the health level the party needs to burst the boss to in order
	/// to break the channel. Hidden when no channel is active.
	/// </summary>
	ColorRect _healthTargetMarker = null!;

	/// <summary>
	/// Blue overlay that shows the boss's current shield as an additional
	/// segment on top of the health bar. Visible whenever CurrentShield > 0.
	/// Anchors are repositioned when health or shield changes so the overlay
	/// always starts at the right edge of the current health fill.
	/// </summary>
	ColorRect _shieldBar = null!;

	// Tracked values so the shield bar can be repositioned whenever either
	// health or shield changes (both affect its anchor position).
	float _trackedHealth;
	float _trackedMaxHealth;
	float _trackedShield;

	/// <param name="bossNameOverride">
	/// When provided, this bar tracks the named character instead of
	/// <see cref="RunState.CurrentBossName"/>. Used for the secondary Astral Twin health bar.
	/// </param>
	public BossHealthBar(string? bossNameOverride = null)
	{
		_bossNameOverride = bossNameOverride;
		_effectIndicatorSize = 40;
	}
	// ── lifecycle ─────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		Visible = false; // hidden until the boss first emits HealthChanged
		AddThemeConstantOverride("separation", 4);

		// ── health panel (top slot in the VBox) ───────────────────────────────
		BuildHealthPanel();

		// ── effect-badge row (below the health bar) ───────────────────────────
		// Wrap in the same side margins as the health panel so the badges
		// align with the bar rather than rendering at the screen edges.
		var effectMargin = new MarginContainer();
		effectMargin.AddThemeConstantOverride("margin_left", SideMargin);
		effectMargin.AddThemeConstantOverride("margin_right", SideMargin);
		effectMargin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		effectMargin.AddChild(EffectBar);
		AddChild(effectMargin);

		// ── subscribe health updates ──────────────────────────────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.HealthChanged),
			Callable.From((string charName, float current, float max) =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (charName == expected)
					UpdateProgress(charName, current, max);
			}));

		// ── subscribe shield updates ──────────────────────────────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(Character.ShieldChanged),
			Callable.From((string charName, float shield, float maxHealth) =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (charName == expected)
					UpdateShieldBar(shield, maxHealth);
			}));

		base._Ready(); // subscribe effect-badge signals

		// ── Court of Reflections — hide/show this bar during the mechanic ───────
		// The bar is hidden while the Countess is invisible and her clones are
		// displayed in world space.  It reappears when the real boss is identified.
		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheCountess.CourtOfReflectionsStarted),
			Callable.From(() =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (expected == GameConstants.CastleBoss2Name)
					Visible = false;
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheCountess.CourtOfReflectionsEnded),
			Callable.From(() =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (expected == GameConstants.CastleBoss2Name)
					Visible = true;
			}));

		// ── Sanguine Siphon health-target marker ──────────────────────────────
		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheBloodPrince.SanguineHealthTargetSet),
			Callable.From((float targetFraction) =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (expected == GameConstants.CastleBoss3Name)
					ShowHealthTargetMarker(targetFraction);
			}));

		GlobalAutoLoad.SubscribeToSignal(
			nameof(TheBloodPrince.SanguineHealthTargetCleared),
			Callable.From(() =>
			{
				var expected = _bossNameOverride ?? (RunState.Instance?.CurrentBossName ?? GameConstants.Boss1Name);
				if (expected == GameConstants.CastleBoss3Name)
					HideHealthTargetMarker();
			}));
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

	/// <summary>
	/// Initialise the bar with known values immediately after it is added to the
	/// scene tree (before the first HealthChanged signal arrives).  Call this
	/// whenever the tracked character's current health is already known — e.g.
	/// for a secondary boss bar created after the boss's _Ready() has already run.
	/// </summary>
	public void Init(string charName, float current, float max)
	{
		UpdateProgress(charName, current, max);
	}

	void UpdateProgress(string charName, float current, float max)
	{
		_trackedHealth = current;
		_trackedMaxHealth = max;
		_bar.Value = Mathf.Clamp(current / max, 0f, 1f);
		_nameLabel.Text = charName;
		_currentHealthLabel.Text = $"{current:F0} / {max:F0}";
		Visible = true;
		RefreshShieldBar(); // reposition shield overlay when health changes
	}

	// ── shield bar ────────────────────────────────────────────────────────────

	void UpdateShieldBar(float shield, float maxHealth)
	{
		_trackedShield = shield;
		if (maxHealth > 0f) _trackedMaxHealth = maxHealth;
		RefreshShieldBar();
	}

	/// <summary>
	/// Repositions <see cref="_shieldBar"/> so it sits immediately to the
	/// right of the current health fill, extending by the shield fraction.
	/// </summary>
	void RefreshShieldBar()
	{
		if (_shieldBar == null) return;

		if (_trackedShield <= 0f || _trackedMaxHealth <= 0f)
		{
			_shieldBar.Visible = false;
			return;
		}

		// The shield occupies the RIGHT edge of the health bar, sized proportional
		// to the remaining shield. This means it always has room to render (even at
		// full health) and naturally shrinks left-to-right as the shield is absorbed.
		var shieldFrac = Mathf.Clamp(_trackedShield / _trackedMaxHealth, 0f, 1f);

		_shieldBar.AnchorLeft  = Mathf.Max(0f, 1f - shieldFrac);
		_shieldBar.AnchorRight = 1f;
		_shieldBar.Visible     = shieldFrac > 0f;
	}

	// ── Sanguine Siphon health-target marker ─────────────────────────────────

	/// <summary>
	/// Positions the golden marker at <paramref name="fraction"/> (0 – 1) along
	/// the health bar, where 0 is fully empty (left) and 1 is full (right).
	/// Since lower health is to the left, a fraction of e.g. 0.40 means the bar
	/// marker sits at the 40 % health mark.
	/// </summary>
	void ShowHealthTargetMarker(float fraction)
	{
		if (_healthTargetMarker == null) return;
		fraction = Mathf.Clamp(fraction, 0f, 1f);
		_healthTargetMarker.AnchorLeft = fraction;
		_healthTargetMarker.AnchorRight = fraction;
		_healthTargetMarker.Visible = true;
	}

	void HideHealthTargetMarker()
	{
		if (_healthTargetMarker != null)
			_healthTargetMarker.Visible = false;
	}

	void BuildHealthPanel()
	{

		// Dark outer panel — matches the same dark-red palette as the bar fill.
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.15f, 0.05f, 0.05f, 0.8f);
		panelStyle.SetCornerRadiusAll(5);
		panelStyle.SetBorderWidthAll(1);
		panelStyle.BorderColor = BorderDefault;
		panelStyle.ContentMarginLeft = 8f;
		panelStyle.ContentMarginRight = 8f;
		panelStyle.ContentMarginTop = 6f;
		panelStyle.ContentMarginBottom = 6f;

		_innerPanel = new PanelContainer();
		_innerPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_innerPanel.AddThemeStyleboxOverride("panel", panelStyle);

		_innerPanel.MouseEntered += () => panelStyle.BorderColor = BorderHovered;
		_innerPanel.MouseExited += () => panelStyle.BorderColor = BorderDefault;

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", SideMargin);
		margin.AddThemeConstantOverride("margin_right", SideMargin);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		AddChild(margin);


		margin.AddChild(_innerPanel);

		// ── main row ──────────────────────────────────────────────────────────
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		_innerPanel.AddChild(row);

		// ── overlay container (bar + overlaid text) ───────────────────────────
		var overlay = new Control();
		overlay.MouseFilter = MouseFilterEnum.Ignore;
		overlay.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		overlay.CustomMinimumSize = new Vector2(0f, 24f);
		row.AddChild(overlay);

		// ── progress bar (fills the entire overlay) ───────────────────────────
		_bar = new ProgressBar();
		_bar.AnchorLeft = 0;
		_bar.AnchorRight = 1;
		_bar.AnchorTop = 0;
		_bar.AnchorBottom = 1;
		_bar.OffsetLeft = _bar.OffsetRight = _bar.OffsetTop = _bar.OffsetBottom = 0;
		_bar.ShowPercentage = false;
		_bar.MinValue = 0f;
		_bar.MaxValue = 1f;
		_bar.Value = 0f;
		_bar.MouseFilter = MouseFilterEnum.Ignore;

		var barBg = new StyleBoxFlat();
		barBg.BgColor = new Color(0.25f, 0.08f, 0.08f, 0.9f);
		barBg.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("background", barBg);

		var barFill = new StyleBoxFlat();
		barFill.BgColor = new Color(0.85f, 0.15f, 0.15f, 0.9f);
		barFill.SetCornerRadiusAll(3);
		_bar.AddThemeStyleboxOverride("fill", barFill);

		overlay.AddChild(_bar);

		// ── health-target marker (Sanguine Siphon channel break threshold) ────
		_healthTargetMarker = new ColorRect();
		_healthTargetMarker.Color = new Color(1.00f, 0.85f, 0.10f, 0.95f); // bright gold
		// Anchored at left=0 initially; repositioned when a channel starts.
		_healthTargetMarker.AnchorTop = 0f;
		_healthTargetMarker.AnchorBottom = 1f;
		_healthTargetMarker.AnchorLeft = 0f;
		_healthTargetMarker.AnchorRight = 0f;
		_healthTargetMarker.OffsetLeft = -1f;
		_healthTargetMarker.OffsetRight = 1f;
		_healthTargetMarker.OffsetTop = 0f;
		_healthTargetMarker.OffsetBottom = 0f;
		_healthTargetMarker.MouseFilter = MouseFilterEnum.Ignore;
		_healthTargetMarker.Visible = false;
		overlay.AddChild(_healthTargetMarker);

		// ── shield overlay (Ice Block / absorb shields) ───────────────────────
		// Icy blue bar that sits to the right of the health fill, representing
		// the boss's current absorb shield. Repositioned whenever health or
		// shield changes via RefreshShieldBar().
		_shieldBar = new ColorRect();
		_shieldBar.Color = new Color(0.40f, 0.72f, 1.0f, 0.80f); // icy blue
		_shieldBar.AnchorTop    = 0f;
		_shieldBar.AnchorBottom = 1f;
		_shieldBar.AnchorLeft   = 0f;
		_shieldBar.AnchorRight  = 0f;
		_shieldBar.OffsetTop    = 0f;
		_shieldBar.OffsetBottom = 0f;
		_shieldBar.OffsetLeft   = 0f;
		_shieldBar.OffsetRight  = 0f;
		_shieldBar.MouseFilter  = MouseFilterEnum.Ignore;
		_shieldBar.Visible      = false;
		overlay.AddChild(_shieldBar);

		// ── name label (left-aligned, over the bar) ───────────────────────────
		_nameLabel = new Label();
		_nameLabel.AnchorLeft = 0;
		_nameLabel.AnchorRight = 1;
		_nameLabel.AnchorTop = 0;
		_nameLabel.AnchorBottom = 1;
		_nameLabel.OffsetLeft = 8;
		_nameLabel.OffsetRight = -40;
		_nameLabel.VerticalAlignment = VerticalAlignment.Center;
		_nameLabel.AddThemeFontSizeOverride("font_size", 14);
		_nameLabel.AddThemeColorOverride("font_color", BarTextColor);
		_nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		overlay.AddChild(_nameLabel);

		// ── health label (right-aligned, over the bar) ────────────────────────
		_currentHealthLabel = new Label();
		_currentHealthLabel.AnchorLeft = 1;
		_currentHealthLabel.AnchorRight = 1;
		_currentHealthLabel.AnchorTop = 0;
		_currentHealthLabel.AnchorBottom = 1;
		_currentHealthLabel.OffsetLeft = -100;
		_currentHealthLabel.OffsetRight = -8;
		_currentHealthLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_currentHealthLabel.VerticalAlignment = VerticalAlignment.Center;
		_currentHealthLabel.AddThemeFontSizeOverride("font_size", 13);
		_currentHealthLabel.AddThemeColorOverride("font_color", BarTextColor);
		_currentHealthLabel.MouseFilter = MouseFilterEnum.Ignore;
		overlay.AddChild(_currentHealthLabel);
	}
}
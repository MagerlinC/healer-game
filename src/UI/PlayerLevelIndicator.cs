using Godot;

namespace healerfantasy.UI;

public partial class PlayerLevelIndicator : HBoxContainer
{
	// ── palette (matches PartyFrame / rest of UI) ─────────────────────────────
	static readonly Color BgDark         = new(0.11f, 0.09f, 0.09f, 0.97f);
	static readonly Color BorderColor    = new(0.45f, 0.38f, 0.28f);          // warm gold-ish
	static readonly Color XpFillColor   = new(0.20f, 0.62f, 0.95f);           // bright blue
	static readonly Color XpBgColor     = new(0.10f, 0.14f, 0.20f);           // dark blue-grey
	static readonly Color LabelColor    = new(0.90f, 0.87f, 0.83f);
	static readonly Color TalentColor   = new(1.00f, 0.82f, 0.20f);           // gold

	Label _levelLabel        = null!;
	Label _xpTextLabel       = null!;
	ProgressBar _xpBar       = null!;
	Label _talentPointsLabel = null!;

	public override void _Ready()
	{
		AddThemeConstantOverride("separation", 10);
		SizeFlagsVertical = SizeFlags.ShrinkCenter;

		// ── Circle (Level) ────────────────────────────────────────────────────
		// PanelContainer auto-sizes its children, solving the centering issue.
		var circlePanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(60, 60),
			SizeFlagsVertical  = SizeFlags.ShrinkCenter,
		};

		var circleStyle = new StyleBoxFlat
		{
			BgColor = BgDark,
			CornerRadiusTopLeft    = 30,
			CornerRadiusTopRight   = 30,
			CornerRadiusBottomLeft = 30,
			CornerRadiusBottomRight = 30,
			ContentMarginLeft   = 0,
			ContentMarginRight  = 0,
			ContentMarginTop    = 0,
			ContentMarginBottom = 0,
		};
		circleStyle.SetBorderWidthAll(2);
		circleStyle.BorderColor = BorderColor;
		circlePanel.AddThemeStyleboxOverride("panel", circleStyle);

		var center = new CenterContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical   = SizeFlags.ExpandFill,
		};

		_levelLabel = new Label
		{
			Text                = PlayerProgressStore.Level.ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment   = VerticalAlignment.Center,
		};
		_levelLabel.AddThemeFontSizeOverride("font_size", 22);
		_levelLabel.AddThemeColorOverride("font_color", LabelColor);

		center.AddChild(_levelLabel);
		circlePanel.AddChild(center);

		// ── Right side ────────────────────────────────────────────────────────
		var rightContainer = new VBoxContainer
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical   = SizeFlags.ShrinkCenter,
		};
		rightContainer.AddThemeConstantOverride("separation", 4);

		// XP bar
		_xpBar = new ProgressBar
		{
			MinValue            = 0,
			MaxValue            = 100,
			Value               = PlayerProgressStore.CurrentXp / (float)PlayerProgressStore.XpToNextLevel(PlayerProgressStore.Level) * 100f,
			ShowPercentage      = false,
			CustomMinimumSize   = new Vector2(0, 10),
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		var xpBg = new StyleBoxFlat { BgColor = XpBgColor };
		xpBg.SetCornerRadiusAll(4);
		var xpFill = new StyleBoxFlat { BgColor = XpFillColor };
		xpFill.SetCornerRadiusAll(4);
		_xpBar.AddThemeStyleboxOverride("background", xpBg);
		_xpBar.AddThemeStyleboxOverride("fill",       xpFill);

		// XP text (e.g. "240 / 1 000 XP")
		_xpTextLabel = new Label
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			HorizontalAlignment = HorizontalAlignment.Left,
		};
		_xpTextLabel.AddThemeFontSizeOverride("font_size", 11);
		_xpTextLabel.AddThemeColorOverride("font_color", new Color(0.60f, 0.72f, 0.85f));

		// Talent points — always added; Visible toggled in Refresh()
		_talentPointsLabel = new Label
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		};
		_talentPointsLabel.AddThemeFontSizeOverride("font_size", 12);
		_talentPointsLabel.AddThemeColorOverride("font_color", TalentColor);

		rightContainer.AddChild(_xpBar);
		rightContainer.AddChild(_xpTextLabel);
		rightContainer.AddChild(_talentPointsLabel);

		// ── Assemble ──────────────────────────────────────────────────────────
		AddChild(circlePanel);
		AddChild(rightContainer);

		Refresh();
	}

	/// <summary>
	/// Re-reads live data from <see cref="PlayerProgressStore"/> and
	/// <see cref="RunState"/> and updates all displayed values.
	/// Call this whenever talent selection changes.
	/// </summary>
	public void Refresh()
	{
		if (_levelLabel == null) return; // called before _Ready

		_levelLabel.Text = PlayerProgressStore.Level.ToString();

		var currentXp = PlayerProgressStore.CurrentXp;
		var xpPerLevel = PlayerProgressStore.XpToNextLevel(PlayerProgressStore.Level);
		_xpBar.Value      = currentXp / (float)xpPerLevel * 100f;
		_xpTextLabel.Text = $"{currentXp:N0} / {xpPerLevel:N0} XP";

		var unspent = PlayerProgressStore.TalentPoints - RunState.Instance.SelectedTalentDefs.Count;
		_talentPointsLabel.Text    = $"✦ {unspent} Talent Point{(unspent == 1 ? "" : "s")} Available";
		_talentPointsLabel.Visible = unspent > 0;
	}
}
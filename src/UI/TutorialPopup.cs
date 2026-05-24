#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// First-time tutorial overlay shown when the player loads the Overworld for the first
/// time (detected via <see cref="PlayerProgressStore.HasSeenTutorial"/>).
///
/// Add as a child of <see cref="OverworldController"/> — the popup sets its own
/// <see cref="CanvasLayer.Visible"/> to <c>false</c> immediately if the tutorial has
/// already been seen, so there is no cost to always adding it unconditionally.
///
/// Dismissing the popup via "Got it!" marks the tutorial as seen in
/// <see cref="PlayerProgressStore"/> and hides the layer.
/// </summary>
public partial class TutorialPopup : CanvasLayer
{
	// ── colours (mirrored from LoadoutController so this class is self-contained) ─
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);

	// ── Godot lifecycle ───────────────────────────────────────────────────────────

	public override void _Ready()
	{
		Layer = 20;

		if (PlayerProgressStore.HasSeenTutorial)
		{
			Visible = false;
			return;
		}

		BuildUI();
	}

	// ── UI construction ───────────────────────────────────────────────────────────

	void BuildUI()
	{
		// Full-screen dimmer — blocks clicks on the world behind.
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.82f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(dimmer);

		// Centred, width-constrained margin.
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 300);
		margin.AddThemeConstantOverride("margin_right", 300);
		margin.AddThemeConstantOverride("margin_top", 60);
		margin.AddThemeConstantOverride("margin_bottom", 60);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		AddChild(margin);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(10);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = PanelBorder;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 28f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 24f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(panel);

		var outerVbox = new VBoxContainer();
		outerVbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(outerVbox);

		// ── Title ─────────────────────────────────────────────────────────────────
		var titleLabel = new Label();
		titleLabel.Text = "Welcome to Keep Us Alive!";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 26);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		outerVbox.AddChild(titleLabel);

		outerVbox.AddChild(MakeSep());

		// ── Scrollable content ────────────────────────────────────────────────────
		outerVbox.AddChild(TutorialContent.Build());

		// ── Close button ──────────────────────────────────────────────────────────
		outerVbox.AddChild(MakeSep());

		var normalStyle = MakeButtonStyle(new Color(0.12f, 0.17f, 0.12f), new Color(0.30f, 0.65f, 0.28f));
		var hoverStyle = MakeButtonStyle(new Color(0.18f, 0.24f, 0.16f), new Color(0.40f, 0.80f, 0.35f));

		var gotItBtn = new Button();
		gotItBtn.Text = "Got it!  Let's go!";
		gotItBtn.CustomMinimumSize = new Vector2(200f, 48f);
		gotItBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		gotItBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		gotItBtn.AddThemeFontSizeOverride("font_size", 16);
		gotItBtn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		gotItBtn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.92f, 0.88f));
		gotItBtn.AddThemeStyleboxOverride("normal", normalStyle);
		gotItBtn.AddThemeStyleboxOverride("hover", hoverStyle);
		gotItBtn.AddThemeStyleboxOverride("pressed", normalStyle);
		gotItBtn.AddThemeStyleboxOverride("focus", normalStyle);
		gotItBtn.Pressed += OnGotItPressed;
		outerVbox.AddChild(gotItBtn);
	}

	// ── event handlers ────────────────────────────────────────────────────────────

	void OnGotItPressed()
	{
		PlayerProgressStore.MarkTutorialSeen();
		Visible = false;
	}

	// ── private helpers ───────────────────────────────────────────────────────────

	HSeparator MakeSep()
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		return sep;
	}

	static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 20f;
		s.ContentMarginTop = s.ContentMarginBottom = 10f;
		return s;
	}

}
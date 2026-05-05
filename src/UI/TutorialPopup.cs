#nullable enable
using Godot;
using healerfantasy;

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
	static readonly Color BodyColor = new(0.82f, 0.78f, 0.72f);

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
		titleLabel.Text = "Welcome to Healer Fantasy!";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 26);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		outerVbox.AddChild(titleLabel);

		outerVbox.AddChild(MakeSep());

		// ── Scrollable content ────────────────────────────────────────────────────
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		outerVbox.AddChild(scroll);

		var contentVbox = new VBoxContainer();
		contentVbox.AddThemeConstantOverride("separation", 20);
		contentVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(contentVbox);

		var dispelKey = GetKeybind("dispel");
		var deflectKey = GetKeybind("deflect");

		contentVbox.AddChild(MakeSection(
			"⚔  Gameplay",
			"As the Healer, your role is to keep your party alive through boss fights. " +
			"Cast healing and shielding spells on your allies — hover over a party member's frame " +
			"and cast to target them individually, or use group spells to help everyone at once.\n\n" +
			"You can also contribute to the fight with offensive spells, dealing damage to the boss " +
			"alongside your party."));

		contentVbox.AddChild(MakeSection(
			"📖  Spells",
			"Pick your spells from the Spellbook before each run. You can equip multiple spells " +
			"and mix different schools of magic to build your ideal loadout.\n\n" +
			"➜  Click the Spellbook tome in the camp to configure your spells!"));

		contentVbox.AddChild(MakeSection(
			"✦  Talents",
			"Talents are passive upgrades that enhance your spells and playstyle. " +
			"Talent points are offered as rewards for killing bosses during a run - use the Talent Board to pick a spell school affinity before starting a run.\n\n" +
			"➜  Click the Talent Board in the camp during a run to change your affinity or to see the talents you've picked so far!"));

		contentVbox.AddChild(MakeSection(
			"🎒  Items",
			"Powerful items drop from bosses during dungeon runs and can greatly boost your " +
			"effectiveness. Between dungeons, visit the Armory at your rest camp to browse and equip them."));

		contentVbox.AddChild(MakeSection(
			$"✦  Dispel  [{dispelKey}]",
			$"Some boss abilities apply harmful debuffs to you or your party. " +
			$"Press [{dispelKey}] to cleanse all harmful effects from the character under your cursor. " +
			$"React accordingly - some debuffs can be deadly if left unchecked, while others might require more strategic decisions!"));

		contentVbox.AddChild(MakeSection(
			$"🛡  Deflect  [{deflectKey}]",
			$"Bosses sometimes telegraph powerful attacks with a visible wind-up. " +
			$"Press [{deflectKey}] at the right moment to parry the attack and reduce its damage to zero. " +
			$"Timing is everything — too early or too late and it won't work!"));

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

	Control MakeSection(string heading, string body)
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var headingLabel = new Label();
		headingLabel.Text = heading;
		headingLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		headingLabel.AddThemeFontSizeOverride("font_size", 15);
		headingLabel.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(headingLabel);

		var bodyLabel = new Label();
		bodyLabel.Text = body;
		bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		bodyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		bodyLabel.AddThemeFontSizeOverride("font_size", 13);
		bodyLabel.AddThemeColorOverride("font_color", BodyColor);
		vbox.AddChild(bodyLabel);

		return vbox;
	}

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

	static string GetKeybind(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);
		return actionName;
	}
}
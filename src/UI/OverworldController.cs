#nullable enable
using Godot;
using healerfantasy;

/// <summary>
/// Root script for the Overworld scene.
///
/// Extends <see cref="LoadoutController"/> so it inherits the full spell/talent
/// overlay system.  Overworld-specific additions:
///   • Library background + walkable player
///   • Run History scroll interactible + overlay (see <c>OverworldController.RunHistory.cs</c>)
///   • Map interactible — opens MapScreen to start or continue the run
///   • First-time tutorial popup (shown once, gated by <see cref="PlayerProgressStore.HasSeenTutorial"/>)
/// </summary>
public partial class OverworldController : LoadoutController
{
	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();

		if (!PlayerProgressStore.HasSeenTutorial)
			ShowTutorialPopup();
	}

	// ── SetupScene ────────────────────────────────────────────────────────────

	protected override void SetupScene()
	{
		var bg = new Sprite2D();
		bg.Texture = GD.Load<Texture2D>(AssetConstants.OverworldBackgroundPath);
		bg.Centered = true;
		bg.Position = new Vector2(1920f / 2f, 1080f / 2f);
		bg.Scale = new Vector2(0.5f, 0.5f);
		AddChild(bg);

		var bgHalfW = bg.Texture.GetWidth() * bg.Scale.X / 2f;
		var bgLeft = bg.Position.X - bgHalfW;
		var bgRight = bg.Position.X + bgHalfW;

		// ── Interactibles ─────────────────────────────────────────────────────
		var spellTome = MakeInteractible(AssetConstants.SpellTomeInteractiblePath,
			new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f);
		var talentBoard = MakeInteractible(AssetConstants.TalentBoardInteractiblePath,
			new Vector2(796f, FloorHeight), new Vector2(0.090f, 0.090f), 50f);
		var historyScroll = MakeInteractible(AssetConstants.RunScrollInteractiblePath,
			new Vector2(696f, FloorHeight), new Vector2(0.055f, 0.055f), 28f);
		var mapItem = MakeInteractible(AssetConstants.MapInteractiblePath,
			new Vector2(525f, FloorHeight), new Vector2(0.100f, 0.100f), 28f);
		mapItem.Scale = new Vector2(1.2f, 1.2f);

		AddChild(spellTome);
		AddChild(talentBoard);
		AddChild(historyScroll);
		AddChild(mapItem);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);
		_interactibles.Add(historyScroll);
		_interactibles.Add(mapItem);

		// ── Run History panel (see OverworldController.RunHistory.cs) ─────────
		_historyPanel = BuildOverlayPanel("Run History", BuildRunHistoryPane());
		_panels.Add(_historyPanel);
		AddChild(_historyPanel);

		// ── Encounter detail modal (layer 15 — above the history panel) ───────
		_detailModalLayer = BuildDetailModal();
		AddChild(_detailModalLayer);

		// ── Player ────────────────────────────────────────────────────────────
		_player = new OverworldPlayer();
		_player.Position = new Vector2(896f, FloorHeight - 15f);
		_player.Scale = new Vector2(1.5f, 1.5f);
		_player.XMin = bgLeft;
		_player.XMax = bgRight;
		AddChild(_player);

		// ── HUD ───────────────────────────────────────────────────────────────
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);
		hud.AddChild(BuildHintLabel());
		hud.AddChild(BuildBackToMenuButton());
		_characterProgressLabel = BuildCharacterProgressLabel();
		hud.AddChild(_characterProgressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		spellTome.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev))
			{
				PlayerProgressStore.MarkSpellbookOpened();
				OpenPanel(_spellPanel!);
			}
		};
		spellTome.MouseEntered += () => _hintLabel!.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited += () => _hintLabel!.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_talentPanel!);
		};
		talentBoard.MouseEntered += () => _hintLabel!.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited += () => _hintLabel!.Text = DefaultHint;

		historyScroll.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenHistoryPanel();
		};
		historyScroll.MouseEntered += () => _hintLabel!.Text = "Run History  •  Click to open";
		historyScroll.MouseExited += () => _hintLabel!.Text = DefaultHint;

		mapItem.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OnOpenMap();
		};
		mapItem.MouseEntered += () => _hintLabel!.Text = "World Map  •  Plan your journey";
		mapItem.MouseExited += () => _hintLabel!.Text = DefaultHint;

		// ── Dev boss popup (Ctrl+Alt+O) — only available in debug builds ────────
		if (OS.IsDebugBuild())
			AddChild(new DevBossPopup());
	}

	void OnOpenMap()
	{
		GetTree().ChangeSceneToFile("res://levels/MapScreen.tscn");
	}

	// ── Tutorial popup ────────────────────────────────────────────────────────

	void ShowTutorialPopup()
	{
		var layer = new CanvasLayer { Layer = 20 };
		AddChild(layer);

		// Full-screen dimmer — blocks clicks on the world behind
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.82f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		// Centred, width-constrained margin
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 300);
		margin.AddThemeConstantOverride("margin_right", 300);
		margin.AddThemeConstantOverride("margin_top", 60);
		margin.AddThemeConstantOverride("margin_bottom", 60);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(margin);

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

		// ── Title ─────────────────────────────────────────────────────────────
		var titleLabel = new Label();
		titleLabel.Text = "Welcome to Healer Fantasy!";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 26);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		outerVbox.AddChild(titleLabel);

		AddHSep(outerVbox);

		// ── Scrollable sections ───────────────────────────────────────────────
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		outerVbox.AddChild(scroll);

		var contentVbox = new VBoxContainer();
		contentVbox.AddThemeConstantOverride("separation", 20);
		contentVbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(contentVbox);

		var dispelKey = GetTutorialKeybind("dispel");
		var deflectKey = GetTutorialKeybind("deflect");

		contentVbox.AddChild(BuildTutorialSection(
			"⚔  Gameplay",
			"As the Healer, your role is to keep your party alive through boss fights. " +
			"Cast healing and shielding spells on your allies — hover over a party member's frame " +
			"and cast to target them individually, or use group spells to help everyone at once.\n\n" +
			"You can also contribute to the fight with offensive spells, dealing damage to the boss " +
			"alongside your party."));

		contentVbox.AddChild(BuildTutorialSection(
			"📖  Spells",
			"Pick your spells from the Spellbook before each run. You can equip multiple spells " +
			"and mix different schools of magic to build your ideal loadout.\n\n" +
			"➜  Click the Spellbook tome in the camp to configure your spells!"));

		contentVbox.AddChild(BuildTutorialSection(
			"✦  Talents",
			"Talents are passive upgrades that enhance your spells and playstyle. " +
			"You earn talent points by levelling up — each boss kill grants experience.\n\n" +
			"➜  Click the Talent Board in the camp to spend your talent points!"));

		contentVbox.AddChild(BuildTutorialSection(
			"🎒  Items",
			"Powerful items drop from bosses during dungeon runs and can greatly boost your " +
			"effectiveness. Between dungeons, visit the Armory at your rest camp to browse and equip them."));

		contentVbox.AddChild(BuildTutorialSection(
			$"✦  Dispel  [{dispelKey}]",
			$"Some boss abilities apply harmful debuffs to you or your party. " +
			$"Press [{dispelKey}] to cleanse all harmful effects from the character under your cursor. " +
			$"React quickly — some debuffs can be deadly if left unchecked!"));

		contentVbox.AddChild(BuildTutorialSection(
			$"🛡  Deflect  [{deflectKey}]",
			$"Bosses sometimes telegraph powerful attacks with a visible wind-up. " +
			$"Press [{deflectKey}] at the right moment to parry the attack and reduce its damage to zero. " +
			$"Timing is everything — too early or too late and it won't work!"));

		// ── Close button ──────────────────────────────────────────────────────
		AddHSep(outerVbox);

		var normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = new Color(0.12f, 0.17f, 0.12f);
		normalStyle.SetCornerRadiusAll(6);
		normalStyle.SetBorderWidthAll(2);
		normalStyle.BorderColor = new Color(0.30f, 0.65f, 0.28f);
		normalStyle.ContentMarginLeft = normalStyle.ContentMarginRight = 20f;
		normalStyle.ContentMarginTop = normalStyle.ContentMarginBottom = 10f;

		var hoverStyle = new StyleBoxFlat();
		hoverStyle.BgColor = new Color(0.18f, 0.24f, 0.16f);
		hoverStyle.SetCornerRadiusAll(6);
		hoverStyle.SetBorderWidthAll(2);
		hoverStyle.BorderColor = new Color(0.40f, 0.80f, 0.35f);
		hoverStyle.ContentMarginLeft = hoverStyle.ContentMarginRight = 20f;
		hoverStyle.ContentMarginTop = hoverStyle.ContentMarginBottom = 10f;

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
		gotItBtn.Pressed += () =>
		{
			PlayerProgressStore.MarkTutorialSeen();
			layer.QueueFree();
		};
		outerVbox.AddChild(gotItBtn);
	}

	Control BuildTutorialSection(string heading, string body)
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
		bodyLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.78f, 0.72f));
		vbox.AddChild(bodyLabel);

		return vbox;
	}

	static string GetTutorialKeybind(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);
		return actionName;
	}

	// ── Main Menu override (no run in progress from Overworld) ────────────────
	protected override void OnMainMenuPressed()
	{
		// From Overworld there is no active run, so just reset and navigate.
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}
}

#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.SpellResources;
using healerfantasy.Talents;
using healerfantasy.UI;

/// <summary>
/// Root script for the Overworld scene.
///
/// The Overworld is a 2D walkable level using a library background.  Three
/// interactible objects are placed in world space:
///
///   • Spell Tome   → opens the Spellbook selection overlay
///   • Talent Board → opens the Talent selection overlay
///   • Run Scroll   → opens the Run History overlay
///
/// A persistent HUD (CanvasLayer 5) provides the "Start Run" and
/// "Main Menu" buttons.  Each selection overlay sits on CanvasLayer 10
/// and is hidden until the corresponding object is clicked.
///
/// All spell/talent selections write directly to <see cref="RunState"/> in
/// real-time, identical to the old tab-based UI.
/// </summary>
public partial class OverworldController : Node2D
{
	// ── shared colours ────────────────────────────────────────────────────────
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);
	static readonly Color ArrowColor = new(0.45f, 0.40f, 0.35f, 0.75f);

	// ── spell panel colours ───────────────────────────────────────────────────
	static readonly Color CardBorderIdle = new(0.28f, 0.22f, 0.16f);
	static readonly Color CardBorderHover = new(0.70f, 0.58f, 0.30f);
	static readonly Color CardBorderEquipped = new(0.98f, 0.82f, 0.15f);
	static readonly Color SlotBorderEmpty = new(0.22f, 0.18f, 0.14f);
	static readonly Color SlotBorderFilled = new(0.60f, 0.48f, 0.22f);

	const float CardW = 92f;
	const float CardH = 116f;
	const float CardIconSz = 64f;

	const float FloorHeight = 780f;

	const string DefaultHint = "Walk up to an object and click it to interact";

	// ── school definitions ────────────────────────────────────────────────────
	static readonly (SpellSchool? School, string Name)[] SpellSchoolTabs =
	{
		(null, "All"),
		(SpellSchool.Holy, "Holy"),
		(SpellSchool.Nature, "Nature"),
		(SpellSchool.Void, "Void"),
		(SpellSchool.Chronomancy, "Chronomancy")
	};

	static readonly (SpellSchool School, string Name, Color Accent)[] TalentSchoolOrder =
	{
		(SpellSchool.Generic, "General", new Color(0.70f, 0.65f, 0.60f)),
		(SpellSchool.Holy, "Holy", new Color(0.95f, 0.85f, 0.40f)),
		(SpellSchool.Nature, "Nature", new Color(0.40f, 0.80f, 0.35f)),
		(SpellSchool.Void, "Void", new Color(0.65f, 0.35f, 0.85f)),
		(SpellSchool.Chronomancy, "Chronomancy", new Color(0.35f, 0.75f, 0.90f))
	};

	// ── runtime state ─────────────────────────────────────────────────────────

	readonly SpellResource?[] _loadout = new SpellResource?[Player.MaxSpellSlots];

	readonly Dictionary<string, (PanelContainer Panel, StyleBoxFlat Border)> _libraryCards = new();

	// Each spell can appear in multiple tabs (e.g. "All" + its school tab), so we
	// store a list of overlay node pairs per spell and update them all together.
	readonly Dictionary<string, List<(ColorRect Overlay, Label Icon)>> _spellLockOverlays = new();
	(PanelContainer Panel, StyleBoxFlat Border, TextureRect Icon)[]? _loadoutSlots;
	readonly List<TalentSlot> _talentSlots = new();
	readonly Dictionary<SpellSchool, Dictionary<int, List<TalentSlot>>> _talentsBySchoolRow = new();

	// ── talent-point HUD ──────────────────────────────────────────────────────
	Label? _talentPointsLabel;
	PlayerLevelIndicator? _characterProgressLabel;

	// ── overworld references ──────────────────────────────────────────────────
	OverworldPlayer? _player;
	CanvasLayer? _spellPanel;
	CanvasLayer? _talentPanel;
	CanvasLayer? _historyPanel;
	VBoxContainer? _historyContent;
	Label? _hintLabel;
	readonly List<Area2D> _interactibles = new();

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventKey kb && kb.Keycode == Key.Escape && kb.Pressed)
		{
			var anyOpen = (_spellPanel?.Visible ?? false)
			              || (_talentPanel?.Visible ?? false)
			              || (_historyPanel?.Visible ?? false);
			if (anyOpen)
			{
				CloseAllPanels();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	public override void _Ready()
	{
		System.Array.Copy(RunState.Instance.SelectedSpells, _loadout, Player.MaxSpellSlots);

		// Tooltip singleton must exist before spell/talent panels are built
		AddChild(new GameTooltip());

		// ── Library background ────────────────────────────────────────────────
		var bg = new Sprite2D();
		bg.Texture = GD.Load<Texture2D>("res://assets/backgrounds/overworld/background.png");
		bg.Centered = true;
		bg.Position = new Vector2(1920f / 2f, 1080f / 2f); // centre of 1920x1080
		// Make smaller
		bg.Scale = new Vector2(0.5f, 0.5f);
		AddChild(bg);

		// Pre-compute background world-space edges for player bounds clamping.
		var bgHalfW = bg.Texture.GetWidth() * bg.Scale.X / 2f;
		var bgLeft = bg.Position.X - bgHalfW;
		var bgRight = bg.Position.X + bgHalfW;

		// ── Interactibles ─────────────────────────────────────────────────────
		// Positions are approximate for a typical library layout.
		// Adjust them in the Godot editor if they don't land on the right spots.
		var spellTome = MakeInteractible("res://assets/interactibles/spell-tome.png",
			new Vector2(996f, FloorHeight - 12f),
			new Vector2(0.080f, 0.080f), 28f);
		var talentBoard = MakeInteractible("res://assets/interactibles/talent-board.png",
			new Vector2(796f, FloorHeight),
			new Vector2(0.090f, 0.090f), 50f);
		var historyScroll = MakeInteractible("res://assets/interactibles/run-history-scroll.png",
			new Vector2(696f, FloorHeight),
			new Vector2(0.055f, 0.055f), 28f);

		var door = MakeInteractible("res://assets/interactibles/door.png",
			new Vector2(490f, FloorHeight - 20f),
			new Vector2(0.100f, 0.100f), 28f);

		AddChild(spellTome);
		AddChild(talentBoard);
		AddChild(historyScroll);
		AddChild(door);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);
		_interactibles.Add(historyScroll);
		_interactibles.Add(door);

		// ── Overlay panels (built once, shown/hidden on demand) ───────────────
		_spellPanel = BuildOverlayPanel("Spellbook", BuildSpellbookPane());
		_talentPanel = BuildOverlayPanel("Talents", BuildTalentPane());
		_historyPanel = BuildOverlayPanel("Run History", BuildRunHistoryPane());
		AddChild(_spellPanel);
		AddChild(_talentPanel);
		AddChild(_historyPanel);

		// ── Player (Last in Render Order) ─────────────────────────────────────
		_player = new OverworldPlayer();
		_player.Position = new Vector2(896f, FloorHeight - 15f);
		_player.Scale = new Vector2(1.5f, 1.5f);
		_player.XMin = bgLeft;
		_player.XMax = bgRight;
		AddChild(_player);

		// ── HUD ───────────────────────────────────────────────────────────────
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);
		hud.AddChild(BuildHintLabel()); // also sets _hintLabel internally
		hud.AddChild(BuildBackToMenuButton());
		_characterProgressLabel = BuildCharacterProgressLabel();
		hud.AddChild(_characterProgressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		spellTome.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_spellPanel);
		};
		spellTome.MouseEntered += () => _hintLabel.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited += () => _hintLabel.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_talentPanel);
		};
		talentBoard.MouseEntered += () => _hintLabel.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited += () => _hintLabel.Text = DefaultHint;

		historyScroll.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_historyPanel, true);
		};
		historyScroll.MouseEntered += () => _hintLabel.Text = "Run History  •  Click to open";
		historyScroll.MouseExited += () => _hintLabel.Text = DefaultHint;

		door.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OnStartRun();
		};
		door.MouseEntered += () => _hintLabel.Text = "Start Run";
		door.MouseExited += () => _hintLabel.Text = DefaultHint;

		// Sync talent slots from RunState (if player is returning from a run)
		SyncTalentSlotsFromRunState();
	}

	// ── panel open / close ────────────────────────────────────────────────────

	void OpenPanel(CanvasLayer panel, bool rebuildHistory = false)
	{
		CloseAllPanels();

		if (rebuildHistory && panel == _historyPanel)
			RebuildHistoryContent();

		panel.Visible = true;
		SetInteractiblesPickable(false);
		_player?.SetPhysicsProcess(false);
	}

	void CloseAllPanels()
	{
		if (_spellPanel != null) _spellPanel.Visible = false;
		if (_talentPanel != null) _talentPanel.Visible = false;
		if (_historyPanel != null) _historyPanel.Visible = false;
		SetInteractiblesPickable(true);
		_player?.SetPhysicsProcess(true);
	}

	void SetInteractiblesPickable(bool pickable)
	{
		foreach (var a in _interactibles)
			a.InputPickable = pickable;
	}

	// ── HUD ───────────────────────────────────────────────────────────────────

	/// <summary>
	/// Builds the bottom-right hint label wrapped in a styled panel.
	/// Sets <see cref="_hintLabel"/> and returns the panel wrapper to add to the HUD.
	/// </summary>
	Control BuildHintLabel()
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.07f, 0.06f, 0.06f, 0.88f);
		style.SetBorderWidthAll(1);
		style.BorderColor = PanelBorder;
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = style.ContentMarginRight = 10f;
		style.ContentMarginTop = style.ContentMarginBottom = 5f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", style);
		panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		// Grow leftward / upward so the panel stays fully on-screen.
		panel.GrowHorizontal = Control.GrowDirection.Begin;
		panel.GrowVertical = Control.GrowDirection.Begin;
		panel.OffsetRight = -12f;
		panel.OffsetBottom = -12f;
		panel.MouseFilter = Control.MouseFilterEnum.Ignore;

		_hintLabel = new Label();
		_hintLabel.Text = DefaultHint;
		_hintLabel.AddThemeFontSizeOverride("font_size", 14);
		_hintLabel.AddThemeColorOverride("font_color", HintColor);
		_hintLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(_hintLabel);

		return panel;
	}

	Control BuildBackToMenuButton()
	{
		var hbox = new HBoxContainer();
		hbox.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		// Grow leftward from the right edge so the full button is visible.
		hbox.GrowHorizontal = Control.GrowDirection.Begin;
		hbox.OffsetRight = -10f;
		hbox.OffsetTop = 10f;
		hbox.AddThemeConstantOverride("separation", 14);

		// Main Menu button
		var menuBtn = new Button();
		menuBtn.Text = "← Main Menu";
		menuBtn.Flat = true;
		menuBtn.CustomMinimumSize = new Vector2(140f, 44f);
		menuBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		menuBtn.AddThemeFontSizeOverride("font_size", 14);
		menuBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		menuBtn.AddThemeColorOverride("font_hover_color", TitleColor);
		menuBtn.Pressed += () => GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
		hbox.AddChild(menuBtn);

		return hbox;
	}

	void OnStartRun()
	{
		RunState.Instance.SetSpells(_loadout);
		GlobalAutoLoad.Reset();
		RunHistoryStore.StartRun();
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	/// <summary>
	/// Builds the top-left HUD label showing character level and XP progress.
	/// </summary>
	PlayerLevelIndicator BuildCharacterProgressLabel()
	{
		var indicator = new PlayerLevelIndicator();
		indicator.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		indicator.OffsetLeft = 20f;
		indicator.OffsetTop = 10f;
		indicator.AddThemeFontSizeOverride("font_size", 18);
		indicator.AddThemeColorOverride("font_color", new Color(0.70f, 0.65f, 0.60f));
		indicator.MouseFilter = Control.MouseFilterEnum.Ignore;
		return indicator;
	}

	// ── overlay panel builder ─────────────────────────────────────────────────

	/// <summary>
	/// Wraps <paramref name="content"/> in a full-screen CanvasLayer overlay
	/// with a dark dimmer, titled panel, and a Close button.
	/// The returned layer is hidden by default.
	/// </summary>
	CanvasLayer BuildOverlayPanel(string title, Control content)
	{
		var layer = new CanvasLayer { Layer = 10 };
		layer.Visible = false;

		// Dark backdrop — also blocks mouse from reaching world-space nodes
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.72f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		// Inset container — gives 80px side margins, 50px top/bottom
		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 80);
		margin.AddThemeConstantOverride("margin_right", 80);
		margin.AddThemeConstantOverride("margin_top", 50);
		margin.AddThemeConstantOverride("margin_bottom", 50);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(margin);

		// Panel background
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = PanelBorder;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 20f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 16f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Title bar
		var titleBar = new HBoxContainer();
		titleBar.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(titleBar);

		var titleLabel = new Label();
		titleLabel.Text = title;
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 22);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleBar.AddChild(titleLabel);

		var closeBtn = new Button();
		closeBtn.Text = "✕  Close";
		closeBtn.Flat = true;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 14);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		closeBtn.Pressed += CloseAllPanels;
		titleBar.AddChild(closeBtn);

		// Separator
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		// Content fills the rest
		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddChild(content);

		// Close on escape
		panel.GuiInput += (ev) =>
		{
			if (ev is InputEventKey kb && kb.Keycode == Key.Escape && kb.Pressed)
				CloseAllPanels();
		};
		return layer;
	}

	// ── interactible factory ──────────────────────────────────────────────────

	Area2D MakeInteractible(string texturePath, Vector2 position,
		Vector2 scale, float collisionRadius)
	{
		var area = new Area2D();
		area.Position = position;
		area.InputPickable = true;
		area.Monitoring = false;
		area.Monitorable = false;

		var sprite = new Sprite2D();
		sprite.Texture = GD.Load<Texture2D>(texturePath);
		sprite.Scale = scale;
		area.AddChild(sprite);

		var collision = new CollisionShape2D();
		collision.Shape = new CircleShape2D { Radius = collisionRadius };
		area.AddChild(collision);

		return area;
	}

	static bool IsLeftClick(InputEvent ev)
	{
		return ev is InputEventMouseButton mb &&
		       mb.ButtonIndex == MouseButton.Left &&
		       mb.Pressed;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// SPELLBOOK PANE  (same logic as before — now shown inside an overlay)
	// ══════════════════════════════════════════════════════════════════════════

	Control BuildSpellbookPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(vbox);

		var libTabs = new TabContainer();
		libTabs.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		libTabs.CustomMinimumSize = new Vector2(0, 280f);
		foreach (var (school, name) in SpellSchoolTabs)
		{
			var pane = BuildSpellLibraryPane(school);
			pane.Name = name;
			libTabs.AddChild(pane);
		}

		vbox.AddChild(libTabs);

		AddHSep(vbox);
		vbox.AddChild(BuildLoadoutRow());

		var hint = new Label();
		hint.Text = "Click a spell to equip or unequip it  •  Click a loadout slot to clear it";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return margin;
	}

	Control BuildSpellLibraryPane(SpellSchool? school)
	{
		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.AddChild(margin);

		var flow = new HFlowContainer();
		flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		flow.AddThemeConstantOverride("h_separation", 10);
		flow.AddThemeConstantOverride("v_separation", 10);
		margin.AddChild(flow);

		var spells = school == null
			? SpellRegistry.AllSpells
			: SpellRegistry.AllSpells.Where(s => s.School == school).ToList();

		if (spells.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No spells available yet!";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 13);
			empty.AddThemeColorOverride("font_color", HintColor);
			flow.AddChild(empty);
		}
		else
		{
			foreach (var spell in spells)
				flow.AddChild(BuildSpellCard(spell));
		}

		return scroll;
	}

	PanelContainer BuildSpellCard(SpellResource spell)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(CardW, CardH);
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var border = new StyleBoxFlat();
		border.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
		border.SetCornerRadiusAll(5);
		border.SetBorderWidthAll(2);
		border.BorderColor = IsEquipped(spell) ? CardBorderEquipped : CardBorderIdle;
		border.ContentMarginLeft = border.ContentMarginRight = 6f;
		border.ContentMarginTop = border.ContentMarginBottom = 6f;
		panel.AddThemeStyleboxOverride("panel", border);

		_libraryCards[spell.Name ?? spell.GetType().Name] = (panel, border);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.AddChild(vbox);

		var iconRect = new TextureRect();
		iconRect.Texture = spell.Icon;
		iconRect.CustomMinimumSize = new Vector2(CardIconSz, CardIconSz);
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(iconRect);

		var schoolLabel = new Label();
		schoolLabel.Text = spell.School.ToString();
		schoolLabel.HorizontalAlignment = HorizontalAlignment.Center;
		schoolLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		schoolLabel.AddThemeFontSizeOverride("font_size", 9);
		schoolLabel.AddThemeColorOverride("font_color", SpellSchoolColor(spell.School));
		schoolLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(schoolLabel);

		var nameLabel = new Label();
		nameLabel.Text = spell.Name ?? "";
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", 10);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(nameLabel);

		// ── Lock overlay (shown when talent requirements aren't met) ──────────
		var lockOverlay = new ColorRect();
		lockOverlay.Color = new Color(0f, 0f, 0f, 0.62f);
		lockOverlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		lockOverlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.AddChild(lockOverlay);

		var lockLabel = new Label();
		lockLabel.Text = "🔒";
		lockLabel.HorizontalAlignment = HorizontalAlignment.Center;
		lockLabel.VerticalAlignment = VerticalAlignment.Center;
		lockLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		lockLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		lockLabel.AddThemeFontSizeOverride("font_size", 22);
		panel.AddChild(lockLabel);

		var spellKey = spell.Name ?? spell.GetType().Name;
		if (!_spellLockOverlays.TryGetValue(spellKey, out var overlayList))
		{
			overlayList = new List<(ColorRect Overlay, Label Icon)>();
			_spellLockOverlays[spellKey] = overlayList;
		}

		overlayList.Add((lockOverlay, lockLabel));

		// Set initial lock state
		var locked = IsSpellLocked(spell);
		lockOverlay.Visible = locked;
		lockLabel.Visible = locked;

		panel.MouseEntered += () =>
		{
			if (IsSpellLocked(spell))
			{
				GameTooltip.Show(GetLockedSpellTooltip(spell));
				return;
			}

			if (!IsEquipped(spell)) border.BorderColor = CardBorderHover;
			GameTooltip.Show(GameTooltip.FormatSpellTooltip(spell));
		};
		panel.MouseExited += () =>
		{
			border.BorderColor = IsEquipped(spell) ? CardBorderEquipped : CardBorderIdle;
			GameTooltip.Hide();
		};
		panel.GuiInput += (ev) =>
		{
			if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				if (!IsSpellLocked(spell))
				{
					ToggleSpell(spell);
				}

				panel.AcceptEvent();
			}
		};

		return panel;
	}

	// ── spell lock helpers ────────────────────────────────────────────────────

	/// <summary>
	/// Returns true if the spell's school-point requirement is not yet met
	/// by the currently selected talents.
	/// </summary>
	bool IsSpellLocked(SpellResource spell)
	{
		if (spell.RequiredSchoolPoints <= 0) return false;
		var invested = _talentSlots.Count(s => s.IsSelected && s.Definition.School == spell.School);
		return invested < spell.RequiredSchoolPoints;
	}

	string GetLockedSpellTooltip(SpellResource spell)
	{
		return
			$"{spell.Name}\n{spell.Description}\nRequires {spell.RequiredSchoolPoints} {spell.School} talent point{(spell.RequiredSchoolPoints > 1 ? "s" : "")} invested.\n" +
			$"({_talentSlots.Count(s => s.IsSelected && s.Definition.School == spell.School)} / {spell.RequiredSchoolPoints} selected)";
	}

	/// <summary>
	/// Refreshes lock overlay visibility for all spell cards based on current talent selection.
	/// Also removes any locked spells from the active loadout.
	/// Call whenever talent selection changes.
	/// </summary>
	void RefreshSpellLockVisuals()
	{
		var loadoutChanged = false;
		foreach (var spell in SpellRegistry.AllSpells)
		{
			var key = spell.Name ?? spell.GetType().Name;
			if (!_spellLockOverlays.TryGetValue(key, out var overlayList)) continue;
			var locked = IsSpellLocked(spell);
			// Update every card instance (e.g. one in the "All" tab, one in the school tab).
			foreach (var nodes in overlayList)
			{
				nodes.Overlay.Visible = locked;
				nodes.Icon.Visible = locked;
			}

			// If a locked spell is currently equipped, remove it from the loadout.
			if (locked && IsEquipped(spell))
			{
				var slot = System.Array.FindIndex(_loadout, s => s?.Name == spell.Name);
				if (slot >= 0)
				{
					_loadout[slot] = null;
					loadoutChanged = true;
				}
			}
		}

		if (loadoutChanged)
		{
			RefreshSpellVisuals();
			RunState.Instance.SetSpells(_loadout);
		}
	}

	Control BuildLoadoutRow()
	{
		_loadoutSlots = new (PanelContainer, StyleBoxFlat, TextureRect)[Player.MaxSpellSlots];

		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 6);

		var label = new Label();
		label.Text = "Loadout";
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		hbox.AddChild(label);

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(10f, 0f);
		hbox.AddChild(spacer);

		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var idx = i;
			var (slotPanel, slotBorder, iconRect) = BuildLoadoutSlot(i);
			_loadoutSlots[i] = (slotPanel, slotBorder, iconRect);

			slotPanel.MouseEntered += () =>
			{
				var s = _loadout[idx];
				if (s != null) GameTooltip.Show(GameTooltip.FormatSpellTooltip(s));
			};
			slotPanel.MouseExited += () => GameTooltip.Hide();
			slotPanel.GuiInput += (ev) =>
			{
				if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
				{
					ClearLoadoutSlot(idx);
					slotPanel.AcceptEvent();
				}
			};
			hbox.AddChild(slotPanel);
		}

		var fill = new Control();
		fill.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(fill);

		RefreshSpellVisuals();
		return hbox;
	}

	(PanelContainer, StyleBoxFlat, TextureRect) BuildLoadoutSlot(int index)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(52f, 52f);
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var border = new StyleBoxFlat();
		border.BgColor = new Color(0.12f, 0.10f, 0.10f, 0.95f);
		border.SetCornerRadiusAll(4);
		border.SetBorderWidthAll(2);
		border.BorderColor = SlotBorderEmpty;
		border.ContentMarginLeft = border.ContentMarginRight = 3f;
		border.ContentMarginTop = border.ContentMarginBottom = 3f;
		panel.AddThemeStyleboxOverride("panel", border);

		var inner = new Control();
		inner.MouseFilter = Control.MouseFilterEnum.Ignore;
		inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		inner.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		panel.AddChild(inner);

		var iconRect = new TextureRect();
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		iconRect.Visible = false;
		inner.AddChild(iconRect);

		var keyLabel = new Label();
		keyLabel.Text = GetKeybindLabel($"spell_{index + 1}");
		keyLabel.AddThemeFontSizeOverride("font_size", 11);
		keyLabel.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.85f));
		keyLabel.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.9f));
		keyLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		keyLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		keyLabel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
		keyLabel.GrowHorizontal = Control.GrowDirection.Begin;
		keyLabel.GrowVertical = Control.GrowDirection.Begin;
		keyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		inner.AddChild(keyLabel);

		return (panel, border, iconRect);
	}

	// ── spell equip helpers ───────────────────────────────────────────────────

	void ToggleSpell(SpellResource spell)
	{
		var slot = System.Array.FindIndex(_loadout, s => s?.Name == spell.Name);
		if (slot >= 0)
			_loadout[slot] = null;
		else
		{
			var empty = System.Array.FindIndex(_loadout, s => s == null);
			if (empty >= 0) _loadout[empty] = spell;
		}

		RefreshSpellVisuals();
		RunState.Instance.SetSpells(_loadout);
		LoadoutPreferences.SaveSpells(_loadout);
	}

	void ClearLoadoutSlot(int index)
	{
		_loadout[index] = null;
		RefreshSpellVisuals();
		RunState.Instance.SetSpells(_loadout);
		LoadoutPreferences.SaveSpells(_loadout);
	}

	bool IsEquipped(SpellResource spell)
	{
		return System.Array.FindIndex(_loadout, s => s?.Name == spell.Name) >= 0;
	}

	void RefreshSpellVisuals()
	{
		foreach (var (name, (_, border)) in _libraryCards)
		{
			var equipped = _loadout.Any(s => s?.Name == name);
			border.BorderColor = equipped ? CardBorderEquipped : CardBorderIdle;
		}

		if (_loadoutSlots == null) return;
		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var spell = _loadout[i];
			var (_, border, iconRect) = _loadoutSlots[i];
			iconRect.Texture = spell?.Icon;
			iconRect.Visible = spell != null;
			border.BorderColor = spell != null ? SlotBorderFilled : SlotBorderEmpty;
		}
	}

	// ══════════════════════════════════════════════════════════════════════════
	// TALENT PANE  (same logic as before — now shown inside an overlay)
	// ══════════════════════════════════════════════════════════════════════════

	Control BuildTalentPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 10);
		margin.AddChild(vbox);

		// ── Talent point budget display ───────────────────────────────────────
		_talentPointsLabel = new Label();
		_talentPointsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_talentPointsLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_talentPointsLabel.AddThemeFontSizeOverride("font_size", 14);
		UpdateTalentPointsLabel();
		vbox.AddChild(_talentPointsLabel);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var hbox = new HBoxContainer();
		hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 0);
		scroll.AddChild(hbox);

		for (var i = 0; i < TalentSchoolOrder.Length; i++)
		{
			if (i > 0)
			{
				var vsep = new VSeparator();
				vsep.AddThemeColorOverride("color", SepColor);
				vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
				hbox.AddChild(vsep);
			}

			var (school, name, accent) = TalentSchoolOrder[i];
			hbox.AddChild(BuildTalentSchoolColumn(school, name, accent));
		}

		vbox.AddChild(scroll);

		var hint = new Label();
		hint.Text = "Click to select a talent  •  Each row requires a selection in the row above";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return margin;
	}

	void UpdateTalentPointsLabel()
	{
		if (_talentPointsLabel == null) return;
		var total = PlayerProgressStore.TalentPoints;
		var selected = _talentSlots.Count(s => s.IsSelected);
		var free = total - selected;

		if (total == 0)
		{
			_talentPointsLabel.Text = "No talent points yet — defeat a boss to level up and earn your first point!";
			_talentPointsLabel.AddThemeColorOverride("font_color", HintColor);
		}
		else
		{
			_talentPointsLabel.Text = $"Talent Points: {free} available  ({selected} / {total} spent)";
			_talentPointsLabel.AddThemeColorOverride("font_color",
				free > 0 ? new Color(0.55f, 0.85f, 0.95f) : new Color(0.70f, 0.65f, 0.55f));
		}
	}

	Control BuildTalentSchoolColumn(SpellSchool school, string colName, Color accent)
	{
		var margin = new MarginContainer();
		margin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddThemeConstantOverride("margin_left", 20);
		margin.AddThemeConstantOverride("margin_right", 20);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);

		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 10);
		col.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(col);

		var header = new Label();
		header.Text = colName;
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddThemeFontSizeOverride("font_size", 16);
		header.AddThemeColorOverride("font_color", accent);
		col.AddChild(header);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(accent.R, accent.G, accent.B, 0.45f));
		col.AddChild(sep);

		var rowGroups = TalentRegistry.AllTalents
			.Where(t => t.School == school)
			.GroupBy(t => t.TalentRow)
			.OrderBy(g => g.Key)
			.ToList();

		if (rowGroups.Count == 0)
		{
			var empty = new Label();
			empty.Text = "Coming soon!";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 12);
			empty.AddThemeColorOverride("font_color", HintColor);
			col.AddChild(empty);
			return margin;
		}

		for (var i = 0; i < rowGroups.Count; i++)
		{
			var rowGroup = rowGroups[i];
			var rowIndex = rowGroup.Key;

			var rowBox = new HBoxContainer();
			rowBox.Alignment = BoxContainer.AlignmentMode.Center;
			rowBox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			rowBox.AddThemeConstantOverride("separation", 12);

			foreach (var def in rowGroup)
			{
				var slot = new TalentSlot(def);
				slot.Toggled += OnTalentSlotToggled;
				_talentSlots.Add(slot);

				if (!_talentsBySchoolRow.ContainsKey(school))
					_talentsBySchoolRow[school] = new Dictionary<int, List<TalentSlot>>();
				if (!_talentsBySchoolRow[school].ContainsKey(rowIndex))
					_talentsBySchoolRow[school][rowIndex] = new List<TalentSlot>();
				_talentsBySchoolRow[school][rowIndex].Add(slot);

				rowBox.AddChild(slot);
			}

			col.AddChild(rowBox);

			if (i < rowGroups.Count - 1)
			{
				var arrow = new Label();
				arrow.Text = "▼";
				arrow.HorizontalAlignment = HorizontalAlignment.Center;
				arrow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				arrow.AddThemeFontSizeOverride("font_size", 18);
				arrow.AddThemeColorOverride("font_color", ArrowColor);
				col.AddChild(arrow);
			}
		}

		return margin;
	}

	// ── talent helpers ────────────────────────────────────────────────────────

	void OnTalentSlotToggled(TalentSlot slot)
	{
		// If the slot was just selected, check we haven't exceeded the budget.
		if (slot.IsSelected)
		{
			var selected = _talentSlots.Count(s => s.IsSelected);
			if (selected > PlayerProgressStore.TalentPoints)
			{
				// Over budget — revert the selection silently.
				slot.SetSelected(false);
				UpdateTalentPointsLabel();
				return;
			}
		}

		ValidateTalentTree(slot.Definition.School);
		CommitTalentsToRunState();
		UpdateTalentPointsLabel();
		_characterProgressLabel?.Refresh();
		// Refresh spell lock overlays — talent choices affect spell availability.
		RefreshSpellLockVisuals();
	}

	void ValidateTalentTree(SpellSchool school)
	{
		if (!_talentsBySchoolRow.TryGetValue(school, out var rowDict)) return;
		foreach (var row in rowDict.Keys.OrderBy(r => r))
		{
			var unlocked = row == 0
			               || rowDict.TryGetValue(row - 1, out var prev) && prev.Any(s => s.IsSelected);
			foreach (var slot in rowDict[row])
			{
				if (!unlocked && slot.IsSelected) slot.SetSelected(false);
				slot.SetLocked(!unlocked);
			}
		}
	}

	void CommitTalentsToRunState()
	{
		var selected = _talentSlots.Where(s => s.IsSelected).Select(s => s.Definition).ToList();
		RunState.Instance.SetTalents(selected);
		LoadoutPreferences.SaveTalents(selected);
	}

	void SyncTalentSlotsFromRunState()
	{
		var active = new HashSet<string>(
			RunState.Instance.SelectedTalentDefs.Select(d => d.Name));
		foreach (var slot in _talentSlots)
			slot.SetSelected(active.Contains(slot.Definition.Name));
		foreach (var (school, _, _) in TalentSchoolOrder)
			ValidateTalentTree(school);

		// Ensure we haven't synced more talents than available points (e.g. after
		// returning from a run that was started before a reset).
		var excess = _talentSlots.Count(s => s.IsSelected) - PlayerProgressStore.TalentPoints;
		if (excess > 0)
		{
			// Deselect the last N selected slots to bring within budget.
			foreach (var slot in _talentSlots.Where(s => s.IsSelected).Reverse().Take(excess))
				slot.SetSelected(false);
			CommitTalentsToRunState();
		}

		UpdateTalentPointsLabel();
		_characterProgressLabel?.Refresh();
		RefreshSpellLockVisuals();
	}

	// ══════════════════════════════════════════════════════════════════════════
	// RUN HISTORY PANE
	// ══════════════════════════════════════════════════════════════════════════

	Control BuildRunHistoryPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(scroll);

		_historyContent = new VBoxContainer();
		_historyContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_historyContent.AddThemeConstantOverride("separation", 16);
		scroll.AddChild(_historyContent);

		// Populated lazily when the panel opens via RebuildHistoryContent()
		RebuildHistoryContent();

		return margin;
	}

	void RebuildHistoryContent()
	{
		if (_historyContent == null) return;

		foreach (var child in _historyContent.GetChildren())
			child.QueueFree();

		var runs = RunHistoryStore.History;

		if (runs.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No runs recorded yet.\nComplete or attempt a run to see your history here.";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AutowrapMode = TextServer.AutowrapMode.Word;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 15);
			empty.AddThemeColorOverride("font_color", HintColor);
			_historyContent.AddChild(empty);
			return;
		}

		// Newest runs first
		for (var i = runs.Count - 1; i >= 0; i--)
		{
			_historyContent.AddChild(BuildRunEntry(i + 1, runs[i]));
			if (i > 0)
			{
				var sep = new HSeparator();
				sep.AddThemeColorOverride("color", SepColor);
				_historyContent.AddChild(sep);
			}
		}
	}

	Control BuildRunEntry(int runNumber, RunHistoryStore.RunRecord run)
	{
		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		// ── Header row ────────────────────────────────────────────────────────
		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(header);

		var runLabel = new Label();
		runLabel.Text = $"Run #{runNumber}  •  {run.CompletedAt:MMM d, yyyy  h:mm tt}";
		runLabel.AddThemeFontSizeOverride("font_size", 14);
		runLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.78f, 0.72f));
		runLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(runLabel);

		// Duration
		var durationLabel = new Label();
		var m = (int)run.Duration.TotalMinutes;
		var s = run.Duration.Seconds;
		durationLabel.Text = $"{m}:{s:D2}";
		durationLabel.AddThemeFontSizeOverride("font_size", 13);
		durationLabel.AddThemeColorOverride("font_color", HintColor);
		header.AddChild(durationLabel);

		// Outcome badge
		var outcome = new Label();
		outcome.Text = run.IsVictory ? "VICTORY" : "DEFEAT";
		outcome.AddThemeFontSizeOverride("font_size", 14);
		outcome.AddThemeColorOverride("font_color",
			run.IsVictory
				? new Color(0.40f, 0.85f, 0.35f)
				: new Color(0.85f, 0.28f, 0.22f));
		header.AddChild(outcome);

		// ── Per-boss rows ─────────────────────────────────────────────────────
		foreach (var enc in run.BossEncounters)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 16);
			vbox.AddChild(row);

			var pad = new Control();
			pad.CustomMinimumSize = new Vector2(24f, 0f);
			row.AddChild(pad);

			var bossName = new Label();
			bossName.Text = enc.BossName;
			bossName.CustomMinimumSize = new Vector2(160f, 0f);
			bossName.AddThemeFontSizeOverride("font_size", 13);
			bossName.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
			row.AddChild(bossName);

			var healLabel = new Label();
			healLabel.Text = $"Healing: {enc.TotalHealing:N0}";
			healLabel.AddThemeFontSizeOverride("font_size", 13);
			healLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.85f, 0.55f));
			row.AddChild(healLabel);

			var dmgDealtLabel = new Label();
			dmgDealtLabel.Text = $"Damage dealt: {enc.TotalDamageDealt:N0}";
			dmgDealtLabel.AddThemeFontSizeOverride("font_size", 13);
			dmgDealtLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.44f, 0.28f));
			row.AddChild(dmgDealtLabel);

			var dmgTakenLabel = new Label();
			dmgTakenLabel.Text = $"Damage taken: {enc.TotalDamageTaken:N0}";
			dmgTakenLabel.AddThemeFontSizeOverride("font_size", 13);
			dmgTakenLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.44f, 0.28f));
			row.AddChild(dmgTakenLabel);
		}

		return vbox;
	}

	// ── shared helpers ────────────────────────────────────────────────────────

	void AddHSep(VBoxContainer parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		parent.AddChild(sep);
	}

	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);
		return actionName.StartsWith("spell_") ? actionName["spell_".Length..] : actionName;
	}

	static Color SpellSchoolColor(SpellSchool school)
	{
		return school switch
		{
			SpellSchool.Holy => new Color(0.95f, 0.85f, 0.40f),
			SpellSchool.Nature => new Color(0.40f, 0.80f, 0.35f),
			SpellSchool.Void => new Color(0.65f, 0.35f, 0.85f),
			SpellSchool.Chronomancy => new Color(0.35f, 0.75f, 0.90f),
			_ => new Color(0.70f, 0.65f, 0.60f)
		};
	}
}
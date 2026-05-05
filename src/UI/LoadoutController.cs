#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.Talents;
using healerfantasy.UI;

namespace healerfantasy;

/// <summary>
/// Abstract base class for scenes that display spell and talent selection UI
/// (currently Overworld and Camp).
///
/// Builds the shared Spellbook and (read-only) Talent overlay panels in
/// <see cref="_Ready"/>, then calls the abstract <see cref="SetupScene"/> for
/// subclasses to add their own background, interactibles, player, and HUD.
///
/// Subclasses add extra panels to <see cref="_panels"/> so ESC / CloseAllPanels
/// can close them automatically.
///
/// Talent panel notes:
///   • Talents are now earned during each run via the victory screen.
///   • The talent panel here is READ-ONLY — it shows icons of currently
///     acquired talents with hover tooltips, organised by school.
///   • The panel refreshes its content every time it is opened so it
///     reflects talents picked up mid-run.
/// </summary>
public abstract partial class LoadoutController : Node2D
{
	// ── shared colours ────────────────────────────────────────────────────────
	protected static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	protected static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	protected static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	protected static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	protected static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);
	protected static readonly Color ArrowColor = new(0.45f, 0.40f, 0.35f, 0.75f);

	// ── spell-card colours ────────────────────────────────────────────────────
	static readonly Color CardBorderIdle = new(0.28f, 0.22f, 0.16f);
	static readonly Color CardBorderHover = new(0.70f, 0.58f, 0.30f);
	static readonly Color CardBorderEquipped = new(0.98f, 0.82f, 0.15f);
	static readonly Color SlotBorderEmpty = new(0.22f, 0.18f, 0.14f);
	static readonly Color SlotBorderFilled = new(0.60f, 0.48f, 0.22f);

	// ── layout constants ──────────────────────────────────────────────────────
	const float CardW = 92f;
	const float CardH = 116f;
	const float CardIconSz = 64f;
	protected const float FloorHeight = 780f;
	protected const string DefaultHint = "Walk up to an object and click it to interact";

	// ── school definitions ────────────────────────────────────────────────────
	protected static readonly (SpellSchool? School, string Name)[] SpellSchoolTabs =
	{
		(null, "All"),
		(SpellSchool.Holy, "Holy"),
		(SpellSchool.Nature, "Nature"),
		(SpellSchool.Void, "Void"),
		(SpellSchool.Chronomancy, "Chronomancy")
	};

	protected static readonly (SpellSchool School, string Name, Color Accent)[] TalentSchoolOrder =
	{
		(SpellSchool.Generic, "General", new Color(0.70f, 0.65f, 0.60f)),
		(SpellSchool.Holy, "Holy", new Color(0.95f, 0.85f, 0.40f)),
		(SpellSchool.Nature, "Nature", new Color(0.40f, 0.80f, 0.35f)),
		(SpellSchool.Void, "Void", new Color(0.65f, 0.35f, 0.85f)),
		(SpellSchool.Chronomancy, "Chronomancy", new Color(0.35f, 0.75f, 0.90f))
	};

	// ── runtime state ─────────────────────────────────────────────────────────
	protected readonly SpellResource?[] _loadout = new SpellResource?[Player.MaxSpellSlots];

	readonly Dictionary<string, (PanelContainer Panel, StyleBoxFlat Border)> _libraryCards = new();
	readonly Dictionary<string, List<(ColorRect Overlay, Label Icon)>> _spellLockOverlays = new();
	(PanelContainer Panel, StyleBoxFlat Border, TextureRect Icon)[]? _loadoutSlots;

	/// <summary>Inner VBoxContainer for the read-only talent panel — repopulated on open.</summary>
	VBoxContainer? _readOnlyTalentContent;

	protected OverworldPlayer? _player;
	protected CanvasLayer? _spellPanel;
	protected CanvasLayer? _talentPanel;
	protected Label? _hintLabel;
	protected readonly List<Area2D> _interactibles = new();

	/// <summary>All overlay panels — registered so ESC / CloseAllPanels can dismiss any of them.</summary>
	protected readonly List<CanvasLayer> _panels = new();

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is InputEventKey kb && kb.Keycode == Key.Escape && kb.Pressed)
		{
			if (_panels.Any(p => p.Visible))
			{
				CloseAllPanels();
				GetViewport().SetInputAsHandled();
			}
		}
	}

	// ── reference resolution ─────────────────────────────────────────────────
	// All world-space positions (background, interactibles, player) are authored
	// for a 1920×1080 canvas.  We scale the root Node2D so they map correctly
	// onto whatever viewport the player is actually using.  CanvasLayer children
	// (panels, HUD) are unaffected — they always render in screen space.
	const float RefW = 1920f;
	const float RefH = 1080f;

	public override void _Ready()
	{
		// ── Viewport-aware world scaling ──────────────────────────────────────
		var vp = GetViewport().GetVisibleRect().Size;
		var s = Mathf.Min(vp.X / RefW, vp.Y / RefH);
		Scale = new Vector2(s, s);
		Position = new Vector2((vp.X - RefW * s) / 2f, (vp.Y - RefH * s) / 2f);

		System.Array.Copy(RunState.Instance.SelectedSpells, _loadout, Player.MaxSpellSlots);
		AddChild(new GameTooltip());

		// Build shared spell + talent panels first so SetupScene can wire
		// interactible click handlers that reference _spellPanel / _talentPanel.
		(_spellPanel, _) = BuildOverlayPanel("Spellbook", BuildSpellbookPane());
		(_talentPanel, _) = BuildOverlayPanel("Talents", BuildReadOnlyTalentPane());
		_panels.Add(_spellPanel);
		_panels.Add(_talentPanel);
		AddChild(_spellPanel);
		AddChild(_talentPanel);

		SetupScene();
		RefreshSpellLockVisuals();
	}

	/// <summary>
	/// Subclasses implement this to add their background, interactibles, player, and HUD.
	/// Called after <see cref="_spellPanel"/> and <see cref="_talentPanel"/> are ready.
	/// </summary>
	protected abstract void SetupScene();

	// ── panel open / close ────────────────────────────────────────────────────

	protected void OpenPanel(CanvasLayer panel)
	{
		CloseAllPanels();
		// Refresh the read-only talent list every time the panel is shown,
		// since talents may have been earned since the scene last loaded.
		if (panel == _talentPanel) RefreshReadOnlyTalentPane();
		panel.Visible = true;
		SetInteractiblesPickable(false);
		_player?.SetPhysicsProcess(false);
	}

	protected void CloseAllPanels()
	{
		foreach (var p in _panels) p.Visible = false;
		SetInteractiblesPickable(true);
		_player?.SetPhysicsProcess(true);
	}

	void SetInteractiblesPickable(bool pickable)
	{
		foreach (var a in _interactibles) a.InputPickable = pickable;
	}

	// ── HUD helpers ───────────────────────────────────────────────────────────

	/// <summary>Builds the bottom-right hint label panel and wires _hintLabel.</summary>
	protected Control BuildHintLabel()
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

	/// <summary>Builds the top-right Main Menu button.</summary>
	protected Control BuildBackToMenuButton()
	{
		var hbox = new HBoxContainer();
		hbox.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		hbox.GrowHorizontal = Control.GrowDirection.Begin;
		hbox.OffsetRight = -10f;
		hbox.OffsetTop = 10f;
		hbox.AddThemeConstantOverride("separation", 14);

		var menuBtn = new Button();
		menuBtn.Text = "← Main Menu";
		menuBtn.Flat = true;
		menuBtn.CustomMinimumSize = new Vector2(140f, 44f);
		menuBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		menuBtn.AddThemeFontSizeOverride("font_size", 14);
		menuBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		menuBtn.AddThemeColorOverride("font_hover_color", TitleColor);
		menuBtn.Pressed += OnMainMenuPressed;
		hbox.AddChild(menuBtn);

		return hbox;
	}

	protected virtual void OnMainMenuPressed()
	{
		// Finalize any in-progress run as a defeat before leaving.
		var runInProgress = RunState.Instance.CompletedDungeons > 0
		                    || RunState.Instance.CurrentBossIndexInDungeon > 0;
		if (runInProgress)
			RunHistoryStore.FinalizeRun(false);

		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── overlay panel builder ─────────────────────────────────────────────────

	protected (CanvasLayer Panel, Label TitleLabel) BuildOverlayPanel(string title, Control content)
	{
		var layer = new CanvasLayer { Layer = 10 };
		layer.Visible = false;

		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.72f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 80);
		margin.AddThemeConstantOverride("margin_right", 80);
		margin.AddThemeConstantOverride("margin_top", 50);
		margin.AddThemeConstantOverride("margin_bottom", 50);
		margin.MouseFilter = Control.MouseFilterEnum.Ignore;
		layer.AddChild(margin);

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

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vbox.AddChild(content);

		panel.GuiInput += (ev) =>
		{
			if (ev is InputEventKey kb && kb.Keycode == Key.Escape && kb.Pressed)
				CloseAllPanels();
		};
		return (layer, titleLabel);
	}

	// ── scene setup helpers ──────────────────────────────────────────────────

	/// <summary>
	/// Scales and positions a full-screen background sprite, then returns the
	/// world-space left and right edges of the visible area for player clamping.
	/// </summary>
	protected (float BgLeft, float BgRight) SetupBackground(string texturePath)
	{
		var camera = GetNode<Camera2D>("Camera2D");
		var viewSize = GetViewport().GetVisibleRect().Size;
		var worldW = viewSize.X / camera.Zoom.X;
		var worldH = viewSize.Y / camera.Zoom.Y;

		var bg = new Sprite2D
		{
			Texture = GD.Load<Texture2D>(texturePath),
			Centered = true,
			Position = camera.Position
		};
		var bgScale = Mathf.Max(worldW / bg.Texture.GetWidth(), worldH / bg.Texture.GetHeight());
		bg.Scale = new Vector2(bgScale, bgScale);
		AddChild(bg);

		return (camera.Position.X - worldW / 2f, camera.Position.X + worldW / 2f);
	}

	/// <summary>
	/// Creates and adds the <see cref="OverworldPlayer"/> to the scene, assigning
	/// movement bounds from the background edges.
	/// </summary>
	protected void SetupPlayer(float xPosition, float bgLeft, float bgRight)
	{
		_player = new OverworldPlayer
		{
			Position = new Vector2(xPosition, FloorHeight - 15f),
			Scale = new Vector2(1.5f, 1.5f),
			XMin = bgLeft,
			XMax = bgRight
		};
		AddChild(_player);
	}

	/// <summary>
	/// Builds the shared HUD canvas layer (hint label, back-to-menu button)
	/// and returns the layer so subclasses can append scene-specific controls.
	/// </summary>
	protected CanvasLayer SetupHud()
	{
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);
		hud.AddChild(BuildHintLabel());
		hud.AddChild(BuildBackToMenuButton());
		return hud;
	}

	/// <summary>
	/// Adds <paramref name="interactible"/> as a child of this node and registers
	/// it in <see cref="_interactibles"/> so panel open/close logic can toggle its
	/// pickability.  Returns the interactible for fluent chaining.
	/// </summary>
	protected T AddInteractible<T>(T interactible) where T : Area2D
	{
		AddChild(interactible);
		_interactibles.Add(interactible);
		return interactible;
	}

	/// <summary>
	/// Wires <paramref name="area"/>'s hover events to update the hint label.
	/// </summary>
	protected void WireHints(Area2D area, string hintText)
	{
		area.MouseEntered += () =>
		{
			if (_hintLabel != null) _hintLabel.Text = hintText;
		};
		area.MouseExited += () =>
		{
			if (_hintLabel != null) _hintLabel.Text = DefaultHint;
		};
	}

	/// <summary>Navigates to the Map Screen scene.</summary>
	protected void OnOpenMap()
	{
		GetTree().ChangeSceneToFile("res://levels/MapScreen.tscn");
	}

	// ══════════════════════════════════════════════════════════════════════════
	// SPELLBOOK PANE
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

			var orderedSpells = spells.OrderBy(s => s.School).ThenBy(s => s.RequiredSchoolPoints).ThenBy(s => s.Name);
			foreach (var spell in orderedSpells) flow.AddChild(BuildSpellCard(spell));
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

		// Lock overlay
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

		var locked = IsSpellLocked(spell);
		lockOverlay.Visible = locked;
		lockLabel.Visible = locked;

		panel.MouseEntered += () =>
		{
			if (IsSpellLocked(spell))
			{
				var tooltip = GetLockedSpellTooltip(spell);
				GameTooltip.Show(tooltip.title, tooltip.desc);
				return;
			}

			if (!IsEquipped(spell)) border.BorderColor = CardBorderHover;
			{
				var tooltip = GameTooltip.FormatSpellTooltip(spell);
				GameTooltip.Show(tooltip.title, tooltip.desc);
			}
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
				if (!IsSpellLocked(spell)) ToggleSpell(spell);
				panel.AcceptEvent();
			}
		};
		return panel;
	}

	// ── spell lock helpers ────────────────────────────────────────────────────

	/// <summary>
	/// A spell is locked if the player has not yet acquired enough talents of its
	/// school during the current run.
	/// </summary>
	bool IsSpellLocked(SpellResource spell)
	{
		if (spell.RequiredSchoolPoints <= 0) return false;
		var invested = RunState.Instance.SelectedTalentDefs.Count(d => d.School == spell.School);
		return invested < spell.RequiredSchoolPoints;
	}

	(string title, string desc) GetLockedSpellTooltip(SpellResource spell)
	{
		var invested = RunState.Instance.SelectedTalentDefs.Count(d => d.School == spell.School);
		return (spell.Name,
			$"{spell.Description}\nRequires {spell.RequiredSchoolPoints} {spell.School} talent" +
			$"{(spell.RequiredSchoolPoints > 1 ? "s" : "")} acquired.\n" +
			$"({invested} / {spell.RequiredSchoolPoints} acquired this run)");
	}

	void RefreshSpellLockVisuals()
	{
		var loadoutChanged = false;
		foreach (var spell in SpellRegistry.AllSpells)
		{
			var key = spell.Name ?? spell.GetType().Name;
			if (!_spellLockOverlays.TryGetValue(key, out var overlayList)) continue;
			var locked = IsSpellLocked(spell);
			foreach (var nodes in overlayList)
			{
				nodes.Overlay.Visible = locked;
				nodes.Icon.Visible = locked;
			}

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
				if (s != null)
				{
					var tooltip = GameTooltip.FormatSpellTooltip(s);
					GameTooltip.Show(tooltip.title, tooltip.desc);
				}
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
		if (slot >= 0) _loadout[slot] = null;
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
	// READ-ONLY TALENT PANE  (acquired talents this run, shown in Camp)
	// ══════════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Builds the outer structure of the read-only talent panel.
	/// The inner <see cref="_readOnlyTalentContent"/> VBoxContainer is populated
	/// (and repopulated on each open) by <see cref="RefreshReadOnlyTalentPane"/>.
	/// </summary>
	Control BuildReadOnlyTalentPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var outerVbox = new VBoxContainer();
		outerVbox.AddThemeConstantOverride("separation", 10);
		margin.AddChild(outerVbox);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		outerVbox.AddChild(scroll);

		_readOnlyTalentContent = new VBoxContainer();
		_readOnlyTalentContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_readOnlyTalentContent.AddThemeConstantOverride("separation", 10);
		scroll.AddChild(_readOnlyTalentContent);

		var hint = new Label();
		hint.Text =
			"Talents are earned by defeating bosses.  Hover an icon to read its effect.\nSet School Affinity above to bias offers (+50%) toward your preferred school.";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AutowrapMode = TextServer.AutowrapMode.Word;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		outerVbox.AddChild(hint);

		return margin;
	}

	/// <summary>
	/// Clears and repopulates <see cref="_readOnlyTalentContent"/> from
	/// <see cref="RunState.Instance.SelectedTalentDefs"/>.
	/// Called each time the talent panel is opened so it reflects the latest
	/// acquisitions.
	///
	/// Always renders the school affinity picker at the top (so players can
	/// set their preferred school even before any talents are acquired).
	/// </summary>
	void RefreshReadOnlyTalentPane()
	{
		if (_readOnlyTalentContent == null) return;
		foreach (var child in _readOnlyTalentContent.GetChildren()) child.QueueFree();

		// ── School affinity picker ─────────────────────────────────────────────
		BuildAffinityPicker(_readOnlyTalentContent);

		var affinitySep = new HSeparator();
		affinitySep.AddThemeColorOverride("color", SepColor);
		_readOnlyTalentContent.AddChild(affinitySep);

		// ── Acquired talent icons ──────────────────────────────────────────────
		var acquired = RunState.Instance.SelectedTalentDefs;

		if (acquired.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No talents acquired yet.\nDefeat bosses during your run to earn talents.";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.AutowrapMode = TextServer.AutowrapMode.Word;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 14);
			empty.AddThemeColorOverride("font_color", HintColor);
			_readOnlyTalentContent.AddChild(empty);
			return;
		}

		// Display talents organised by school (only schools with at least one acquired talent).
		foreach (var (school, schoolName, accent) in TalentSchoolOrder)
		{
			var schoolTalents = acquired.Where(t => t.School == school).ToList();
			if (schoolTalents.Count == 0) continue;

			// School header
			var header = new Label();
			header.Text = schoolName;
			header.AddThemeFontSizeOverride("font_size", 13);
			header.AddThemeColorOverride("font_color", accent);
			_readOnlyTalentContent.AddChild(header);

			var sep = new HSeparator();
			sep.AddThemeColorOverride("color", new Color(accent.R, accent.G, accent.B, 0.35f));
			_readOnlyTalentContent.AddChild(sep);

			// Icon flow
			var flow = new HFlowContainer();
			flow.AddThemeConstantOverride("h_separation", 8);
			flow.AddThemeConstantOverride("v_separation", 8);
			flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			_readOnlyTalentContent.AddChild(flow);

			foreach (var def in schoolTalents)
			{
				var iconStyle = new StyleBoxFlat();
				iconStyle.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
				iconStyle.SetCornerRadiusAll(4);
				iconStyle.SetBorderWidthAll(2);
				iconStyle.BorderColor = new Color(accent.R * 0.7f, accent.G * 0.7f, accent.B * 0.7f);

				var iconPanel = new PanelContainer();
				iconPanel.CustomMinimumSize = new Vector2(58f, 58f);
				iconPanel.AddThemeStyleboxOverride("panel", iconStyle);
				iconPanel.MouseFilter = Control.MouseFilterEnum.Stop;
				iconPanel.MouseDefaultCursorShape = Control.CursorShape.Arrow;

				var icon = new TextureRect();
				icon.Texture = GD.Load<Texture2D>(def.IconPath);
				icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
				icon.MouseFilter = Control.MouseFilterEnum.Ignore;
				iconPanel.AddChild(icon);

				var capturedDef = def;
				iconPanel.MouseEntered += () => GameTooltip.Show(capturedDef.Name, capturedDef.Description);
				iconPanel.MouseExited += () => GameTooltip.Hide();

				flow.AddChild(iconPanel);
			}

			// Small spacer between schools
			var spacer = new Control();
			spacer.CustomMinimumSize = new Vector2(0f, 4f);
			_readOnlyTalentContent.AddChild(spacer);
		}
	}

	/// <summary>
	/// Builds and appends the school affinity picker row into <paramref name="parent"/>.
	///
	/// Four tome icons (Holy / Nature / Void / Chronomancy) are displayed in a
	/// horizontal row.  Clicking a tome sets <see cref="RunState.SchoolAffinity"/>
	/// to that school (+50% weight bias on the victory screen).  Clicking the
	/// currently selected tome clears the affinity.  The active tome shows a gold
	/// border; inactive tomes show a dim border.
	/// </summary>
	void BuildAffinityPicker(VBoxContainer parent)
	{
		// Section header
		var header = new Label();
		header.Text = "School Affinity";
		header.AddThemeFontSizeOverride("font_size", 13);
		header.AddThemeColorOverride("font_color", TitleColor);
		parent.AddChild(header);


		// Row of 4 tomes — one per school that can have affinity.
		// SpellSchool.Generic is excluded (it's a catch-all, not a real choice).
		var tomeRow = new HBoxContainer();
		tomeRow.AddThemeConstantOverride("separation", 12);
		parent.AddChild(tomeRow);

		// We'll keep references so we can rebuild borders after a click without
		// re-creating all nodes (the whole pane is rebuilt on next open anyway).
		var tomeSchools = new[]
		{
			SpellSchool.Holy, SpellSchool.Nature,
			SpellSchool.Void, SpellSchool.Chronomancy
		};

		var tomePanels = new List<(SpellSchool School, PanelContainer Panel, StyleBoxFlat Style)>();

		foreach (var school in tomeSchools)
		{
			var (_, schoolName, accent) = TalentSchoolOrder.First(e => e.School == school);

			var isSelected = RunState.Instance.SchoolAffinity == school;

			var tomeStyle = new StyleBoxFlat();
			tomeStyle.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
			tomeStyle.SetCornerRadiusAll(6);
			tomeStyle.SetBorderWidthAll(2);
			tomeStyle.BorderColor = isSelected
				? PanelBorder // gold
				: new Color(0.28f, 0.24f, 0.16f); // dim
			tomeStyle.ContentMarginLeft = tomeStyle.ContentMarginRight = 6f;
			tomeStyle.ContentMarginTop = tomeStyle.ContentMarginBottom = 6f;

			var tomePanel = new PanelContainer();
			tomePanel.CustomMinimumSize = new Vector2(80f, 100f);
			tomePanel.AddThemeStyleboxOverride("panel", tomeStyle);
			tomePanel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			tomePanel.MouseFilter = Control.MouseFilterEnum.Stop;

			var inner = new VBoxContainer();
			inner.AddThemeConstantOverride("separation", 4);
			inner.MouseFilter = Control.MouseFilterEnum.Ignore;
			tomePanel.AddChild(inner);

			var tomeIcon = new TextureRect();
			tomeIcon.Texture = GD.Load<Texture2D>(AssetConstants.TalentTomePath(school));
			tomeIcon.CustomMinimumSize = new Vector2(64f, 64f);
			tomeIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			tomeIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			tomeIcon.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			tomeIcon.MouseFilter = Control.MouseFilterEnum.Ignore;
			inner.AddChild(tomeIcon);

			var nameLabel = new Label();
			nameLabel.Text = schoolName;
			nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
			nameLabel.AddThemeFontSizeOverride("font_size", 11);
			nameLabel.AddThemeColorOverride("font_color", accent);
			nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			inner.AddChild(nameLabel);

			tomePanels.Add((school, tomePanel, tomeStyle));
			tomeRow.AddChild(tomePanel);

			// Hover tooltip
			var capturedSchool = school;
			var capturedName = schoolName;
			tomePanel.MouseEntered += () =>
				GameTooltip.Show(capturedName + " Affinity",
					$"Set your school affinity to {capturedName}.\nDouble the chance of at least one {capturedName} talent appearing in each offer.");
			tomePanel.MouseExited += () => GameTooltip.Hide();

			// Click: toggle affinity
			tomePanel.GuiInput += ev =>
			{
				if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;

				var alreadySelected = RunState.Instance.SchoolAffinity == capturedSchool;
				RunState.Instance.SetSchoolAffinity(alreadySelected ? null : capturedSchool);

				// Update all tome borders immediately without rebuilding the whole pane.
				foreach (var (s, p, style) in tomePanels)
				{
					var (_, _, a) = TalentSchoolOrder.First(e => e.School == s);
					style.BorderColor = RunState.Instance.SchoolAffinity == s
						? PanelBorder
						: new Color(0.28f, 0.24f, 0.16f);
					_ = p;
					_ = a; // suppress unused-var warnings
				}
			};
		}
	}

	// ── shared helpers ────────────────────────────────────────────────────────

	protected void AddHSep(VBoxContainer parent)
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

	protected static Color SpellSchoolColor(SpellSchool school)
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
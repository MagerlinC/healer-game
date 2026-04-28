#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.SpellResources;
using SpellResource = healerfantasy.SpellResources.SpellResource;

/// <summary>
/// Full-screen spellbook panel.
///
/// • Press B (or click ✕) to open/close. Game is paused while open.
/// • The top section shows the full spell library in per-school tabs
///   (All / Holy / Nature / Void / Chronomancy).
/// • The bottom section shows the 6-slot loadout that maps to the action bar.
/// • Clicking an unequipped spell adds it to the first empty loadout slot.
/// • Clicking an equipped spell (in the library OR the loadout slot) removes it.
/// • Changes are committed to the player and the action bar is rebuilt on close.
///
/// Wiring: call <see cref="Init"/> from <c>World._Ready</c> after the Player
/// and GameUI nodes are resolved.
/// </summary>
public partial class SpellbookSelector : CanvasLayer
{
	// ── colours / sizes ──────────────────────────────────────────────────────
	static readonly Color OverlayBg = new(0.00f, 0.00f, 0.00f, 0.72f);
	static readonly Color PanelBg = new(0.10f, 0.08f, 0.07f, 0.98f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);

	static readonly Color CardBorderIdle = new(0.28f, 0.22f, 0.16f);
	static readonly Color CardBorderHover = new(0.70f, 0.58f, 0.30f);
	static readonly Color CardBorderEquipped = new(0.98f, 0.82f, 0.15f); // gold

	static readonly Color SlotBorderEmpty = new(0.22f, 0.18f, 0.14f);
	static readonly Color SlotBorderFilled = new(0.60f, 0.48f, 0.22f);

	const float CardW = 92f;
	const float CardH = 116f;
	const float CardIconSize = 64f;

	/// <summary>School tabs in display order. null = "All spells".</summary>
	static readonly (SpellSchool? School, string TabName)[] SchoolOrder =
	{
		(null, "All"),
		(SpellSchool.Holy, "Holy"),
		(SpellSchool.Nature, "Nature"),
		(SpellSchool.Void, "Void"),
		(SpellSchool.Chronomancy, "Chronomancy"),
		(SpellSchool.Generic, "Generic") // Always-available spells shown read-only
	};

	// ── state ────────────────────────────────────────────────────────────────
	Player? _player;
	GameUI? _gameUI;
	Control? _overlay;
	bool _isOpen;

	/// <summary>Working copy of the loadout, edited until the panel closes.</summary>
	readonly SpellResource?[] _loadout = new SpellResource?[Player.MaxSpellSlots];

	/// <summary>Library card UI refs, keyed by spell name for quick visual refresh.</summary>
	readonly Dictionary<string, (PanelContainer Panel, StyleBoxFlat Border)> _libraryCardUis = new();

	/// <summary>Loadout slot UI refs, one per slot index.</summary>
	(PanelContainer Panel, StyleBoxFlat Border, TextureRect IconRect)[]? _loadoutSlotUis;

	// ── public API ───────────────────────────────────────────────────────────
	/// <summary>
	/// Must be called once after the node enters the scene tree.
	/// Links the panel to the <paramref name="player"/> and <paramref name="gameUI"/>
	/// it manages.
	/// </summary>
	public void Init(Player player, GameUI gameUI)
	{
		_player = player;
		_gameUI = gameUI;
	}

	// ── lifecycle ────────────────────────────────────────────────────────────
	public override void _Ready()
	{
		// One layer above TalentSelector (15) so they don't overlap.
		Layer = 16;
		ProcessMode = ProcessModeEnum.Always;

		_overlay = BuildOverlay();
		AddChild(_overlay);
		_overlay.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

		if (key.PhysicalKeycode == Key.B)
		{
			Toggle();
			GetViewport().SetInputAsHandled();
		}
		else if (key.PhysicalKeycode == Key.Escape && _isOpen)
		{
			Close();
			GetViewport().SetInputAsHandled();
		}
	}

	// ── open / close ─────────────────────────────────────────────────────────
	void Toggle()
	{
		if (_isOpen) Close();
		else Open();
	}

	void Open()
	{
		if (_player == null) return;
		if (GetTree().Paused) return; // another panel (e.g. Talents) is already open

		// Copy the player's current loadout into our working copy
		Array.Copy(_player.EquippedSpells, _loadout, Player.MaxSpellSlots);
		RefreshAllVisuals();

		_isOpen = true;
		_overlay!.Visible = true;
		GetTree().Paused = true;
	}

	void Close()
	{
		_isOpen = false;
		_overlay!.Visible = false;
		GetTree().Paused = false;

		// Commit the working loadout to the player and refresh the action bar
		Array.Copy(_loadout, _player!.EquippedSpells, Player.MaxSpellSlots);
		_gameUI!.RebuildActionBar(_player.EquippedSpells);
	}

	// ── equip / unequip logic ─────────────────────────────────────────────────

	void OnLibraryCardClicked(SpellResource spell)
	{
		var slot = FindSlotWithSpell(spell);
		if (slot >= 0)
		{
			// Already equipped → remove it
			_loadout[slot] = null;
		}
		else
		{
			// Not equipped → place in first empty slot (if any)
			var empty = FindFirstEmptySlot();
			if (empty >= 0)
				_loadout[empty] = spell;
			// If all slots are full we silently do nothing
		}

		RefreshAllVisuals();
	}

	void OnLoadoutSlotClicked(int slotIndex)
	{
		_loadout[slotIndex] = null;
		RefreshAllVisuals();
	}

	/// <returns>Index of the loadout slot containing <paramref name="spell"/>, or -1.</returns>
	int FindSlotWithSpell(SpellResource spell)
	{
		return Array.FindIndex(_loadout, s => s?.Name == spell.Name);
	}

	int FindFirstEmptySlot()
	{
		return Array.FindIndex(_loadout, s => s == null);
	}

	bool IsEquipped(SpellResource spell)
	{
		return FindSlotWithSpell(spell) >= 0;
	}

	// ── visual refresh ────────────────────────────────────────────────────────

	void RefreshAllVisuals()
	{
		// Update library card borders
		foreach (var (name, (_, border)) in _libraryCardUis)
		{
			var equipped = _loadout.Any(s => s?.Name == name);
			border.BorderColor = equipped ? CardBorderEquipped : CardBorderIdle;
		}

		// Update loadout slot icons and borders
		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var spell = _loadout[i];
			var (panel, border, iconRect) = _loadoutSlotUis![i];

			iconRect.Texture = spell?.Icon;
			iconRect.Visible = spell != null;
			border.BorderColor = spell != null ? SlotBorderFilled : SlotBorderEmpty;
			panel.TooltipText = spell?.Name ?? $"Slot {i + 1} (empty)";
		}
	}

	// ── UI construction ───────────────────────────────────────────────────────

	Control BuildOverlay()
	{
		var overlay = new ColorRect();
		overlay.Color = OverlayBg;
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		overlay.AddChild(BuildPanel());
		return overlay;
	}

	PanelContainer BuildPanel()
	{
		var panel = new PanelContainer();

		var style = new StyleBoxFlat();
		style.BgColor = PanelBg;
		style.SetCornerRadiusAll(10);
		style.SetBorderWidthAll(2);
		style.BorderColor = PanelBorder;
		style.ContentMarginLeft = style.ContentMarginRight = 24f;
		style.ContentMarginTop = 18f;
		style.ContentMarginBottom = 24f;
		panel.AddThemeStyleboxOverride("panel", style);

		panel.AnchorLeft = panel.AnchorRight = 0.5f;
		panel.AnchorTop = panel.AnchorBottom = 0.5f;
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// Title
		vbox.AddChild(BuildTitleRow());

		// Separator
		AddSeparator(vbox);

		// Spell library tabs
		vbox.AddChild(BuildLibraryTabs());

		// Separator
		AddSeparator(vbox);

		// Loadout row
		vbox.AddChild(BuildLoadoutSection());

		// Footer hint
		var hint = new Label();
		hint.Text = "Click a spell to equip or remove it  •  Click a loadout slot to clear it  •  [B] or [Esc] to close";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return panel;
	}

	// ── library tabs ──────────────────────────────────────────────────────────

	TabContainer BuildLibraryTabs()
	{
		var tabs = new TabContainer();
		tabs.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		// Minimum height: enough for two rows of cards plus margins
		tabs.CustomMinimumSize = new Vector2(760f, 420f);

		foreach (var (school, tabName) in SchoolOrder)
		{
			var pane = BuildSchoolPane(school);
			pane.Name = tabName;
			tabs.AddChild(pane);
		}

		return tabs;
	}

	/// <summary>
	/// Builds the scroll pane for one school tab.
	/// <paramref name="school"/> null means show all spells regardless of school.
	/// Generic spells get a read-only presentation — they cannot be equipped or
	/// unequipped because they are always available via the generic action bar.
	/// </summary>
	Control BuildSchoolPane(SpellSchool? school)
	{
		// Generic tab: show always-available spells in a read-only layout.
		if (school == SpellSchool.Generic)
			return BuildGenericPane();

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
			empty.Text = "No spells available yet — coming soon!";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 13);
			empty.AddThemeColorOverride("font_color", HintColor);
			flow.AddChild(empty);
			return scroll;
		}

		foreach (var spell in spells)
			flow.AddChild(BuildLibraryCard(spell));

		return scroll;
	}

	/// <summary>
	/// Builds the read-only Generic tab showing always-available spells.
	/// These cards cannot be clicked to equip/unequip.
	/// </summary>
	Control BuildGenericPane()
	{
		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 12);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		margin.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		margin.AddChild(vbox);
		scroll.AddChild(margin);

		// Header note
		var note = new Label();
		note.Text =
			"These spells are always available and cannot be removed.\nActivate them from the generic action bar (right of the main bar).";
		note.HorizontalAlignment = HorizontalAlignment.Center;
		note.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		note.AddThemeFontSizeOverride("font_size", 12);
		note.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(note);

		var flow = new HFlowContainer();
		flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		flow.AddThemeConstantOverride("h_separation", 10);
		flow.AddThemeConstantOverride("v_separation", 10);
		vbox.AddChild(flow);

		foreach (var spell in SpellRegistry.GenericSpells)
			flow.AddChild(BuildReadOnlyCard(spell));

		return scroll;
	}

	/// <summary>
	/// Builds a spell card that is displayed but cannot be clicked.
	/// Used for always-available generic spells in the Generic tab.
	/// </summary>
	PanelContainer BuildReadOnlyCard(SpellResource spell)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(CardW, CardH);
		// Default cursor — signals this is not interactive
		panel.MouseDefaultCursorShape = Control.CursorShape.Arrow;

		var border = new StyleBoxFlat();
		border.BgColor = new Color(0.09f, 0.07f, 0.10f, 0.97f);
		border.SetCornerRadiusAll(5);
		border.SetBorderWidthAll(2);
		// Always gold: these are always "equipped"
		border.BorderColor = CardBorderEquipped;
		border.ContentMarginLeft = border.ContentMarginRight = 6f;
		border.ContentMarginTop = border.ContentMarginBottom = 6f;
		panel.AddThemeStyleboxOverride("panel", border);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.AddChild(vbox);

		// Icon
		var iconRect = new TextureRect();
		iconRect.Texture = spell.Icon;
		iconRect.CustomMinimumSize = new Vector2(CardIconSize, CardIconSize);
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(iconRect);

		// "Always Available" badge instead of a school label
		var badge = new Label();
		badge.Text = "Always Available";
		badge.HorizontalAlignment = HorizontalAlignment.Center;
		badge.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		badge.AddThemeFontSizeOverride("font_size", 9);
		badge.AddThemeColorOverride("font_color", new Color(0.70f, 0.60f, 0.85f)); // soft purple
		badge.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(badge);

		// Name
		var nameLabel = new Label();
		nameLabel.Text = spell.Name ?? "";
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", 10);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(nameLabel);

		// Tooltip (hover only — no click handler)
		var tooltipText = GameTooltip.FormatSpellTooltip(spell);
		panel.MouseEntered += () => GameTooltip.Show(tooltipText.title, tooltipText.desc);
		panel.MouseExited += () => GameTooltip.Hide();

		return panel;
	}

	PanelContainer BuildLibraryCard(SpellResource spell)
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(CardW, CardH);
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var border = new StyleBoxFlat();
		border.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
		border.SetCornerRadiusAll(5);
		border.SetBorderWidthAll(2);
		border.BorderColor = CardBorderIdle;
		border.ContentMarginLeft = border.ContentMarginRight = 6f;
		border.ContentMarginTop = border.ContentMarginBottom = 6f;
		panel.AddThemeStyleboxOverride("panel", border);

		// Register by spell name so RefreshAllVisuals can find this card
		_libraryCardUis[spell.Name ?? spell.GetType().Name] = (panel, border);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 4);
		vbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.AddChild(vbox);

		// Icon
		var iconRect = new TextureRect();
		iconRect.Texture = spell.Icon;
		iconRect.CustomMinimumSize = new Vector2(CardIconSize, CardIconSize);
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(iconRect);

		// School label
		var schoolLabel = new Label();
		schoolLabel.Text = spell.School.ToString();
		schoolLabel.HorizontalAlignment = HorizontalAlignment.Center;
		schoolLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		schoolLabel.AddThemeFontSizeOverride("font_size", 9);
		schoolLabel.AddThemeColorOverride("font_color", SchoolColor(spell.School));
		schoolLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(schoolLabel);

		// Name
		var nameLabel = new Label();
		nameLabel.Text = spell.Name ?? "";
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", 10);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.84f, 0.78f));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		vbox.AddChild(nameLabel);

		// Hover, tooltip, and click events
		var tooltipText = GameTooltip.FormatSpellTooltip(spell);
		panel.MouseEntered += () =>
		{
			if (!IsEquipped(spell))
				border.BorderColor = CardBorderHover;
			GameTooltip.Show(tooltipText.title, tooltipText.desc);
		};
		panel.MouseExited += () =>
		{
			border.BorderColor = IsEquipped(spell) ? CardBorderEquipped : CardBorderIdle;
			GameTooltip.Hide();
		};
		panel.GuiInput += (@event) =>
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				OnLibraryCardClicked(spell);
				panel.AcceptEvent();
			}
		};

		return panel;
	}

	// ── loadout section ───────────────────────────────────────────────────────

	Control BuildLoadoutSection()
	{
		_loadoutSlotUis = new (PanelContainer, StyleBoxFlat, TextureRect)[Player.MaxSpellSlots];

		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 6);

		// "Loadout" label
		var label = new Label();
		label.Text = "Loadout";
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 13);
		label.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		hbox.AddChild(label);

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(10f, 0f);
		hbox.AddChild(spacer);

		// Six loadout slots
		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var slotIndex = i; // capture for lambda
			var (slotPanel, slotBorder, iconRect) = BuildLoadoutSlot(i);
			_loadoutSlotUis[i] = (slotPanel, slotBorder, iconRect);

			slotPanel.MouseEntered += () =>
			{
				var s = _loadout[slotIndex];
				if (s != null)
				{
					var tooltip = GameTooltip.FormatSpellTooltip(s);
					GameTooltip.Show(tooltip.title, tooltip.desc);
				}
			};
			slotPanel.MouseExited += () => GameTooltip.Hide();
			slotPanel.GuiInput += (@event) =>
			{
				if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
				{
					OnLoadoutSlotClicked(slotIndex);
					slotPanel.AcceptEvent();
				}
			};

			hbox.AddChild(slotPanel);
		}

		// Flexible spacer so the slots stay left-aligned in a wide panel
		var fill = new Control();
		fill.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.AddChild(fill);

		return hbox;
	}

	(PanelContainer Panel, StyleBoxFlat Border, TextureRect IconRect) BuildLoadoutSlot(int slotIndex)
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

		// Spell icon (hidden when empty)
		var iconRect = new TextureRect();
		iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		iconRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		iconRect.Visible = false;
		inner.AddChild(iconRect);

		// Key label in bottom-right corner (reads live from InputMap for rebind support)
		var keyLabel = new Label();
		keyLabel.Text = GetKeybindLabel($"spell_{slotIndex + 1}");
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

	// ── helpers ───────────────────────────────────────────────────────────────

	Control BuildTitleRow()
	{
		var hbox = new HBoxContainer();

		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(28, 0);
		hbox.AddChild(spacer);

		var title = new Label();
		title.Text = "Spellbook";
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 22);
		title.AddThemeColorOverride("font_color", TitleColor);
		hbox.AddChild(title);

		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.Flat = true;
		closeBtn.CustomMinimumSize = new Vector2(28, 28);
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.9f, 0.55f));
		closeBtn.Pressed += Close;
		hbox.AddChild(closeBtn);

		return hbox;
	}

	void AddSeparator(VBoxContainer parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		parent.AddChild(sep);
	}

	/// <summary>
	/// Reads the first bound key for <paramref name="actionName"/> from the live
	/// InputMap, so player rebinds are reflected without any code changes.
	/// </summary>
	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
			return OS.GetKeycodeString(key.PhysicalKeycode);

		return actionName.StartsWith("spell_")
			? actionName["spell_".Length..]
			: actionName;
	}

	/// <summary>Returns an accent colour for the school label on each spell card.</summary>
	static Color SchoolColor(SpellSchool school)
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
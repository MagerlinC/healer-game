#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources;
using healerfantasy.SpellResources.Void;
using healerfantasy.Talents;
using healerfantasy.UI;

/// <summary>
/// Hidden developer popup for launching test boss fights directly from the Overworld.
///
/// Toggle open / closed with Ctrl+Alt+O (also closeable with Escape).
///
/// Guarded by <see cref="OS.IsDebugBuild"/> — the entire popup is a no-op in release
/// builds so players cannot access it.
///
/// Three sub-panels are available from the popup:
///   • 🎭 Talents — opens TalentSelector in dev mode (writes to RunState directly)
///   • ✨ Spells  — inline spell picker; click to equip/unequip from every school
///   • 🗡 Items   — item browser + EquipmentPane; click items to add to inventory
///
/// After configuring a loadout, type a boss name and press "Launch Test Fight".
/// The run returns to Overworld after the fight and resets state.
/// </summary>
public partial class DevBossPopup : CanvasLayer
{
	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color DevAccent = new(0.35f, 0.75f, 0.90f);
	static readonly Color DevSepColor = new(0.35f, 0.75f, 0.90f, 0.40f);
	static readonly Color PanelBg = new(0.07f, 0.06f, 0.06f, 0.97f);
	static readonly Color HintColor = new(0.35f, 0.35f, 0.35f);
	static readonly Color TitleColor = new(0.35f, 0.85f, 1.00f);
	static readonly Color GoldColor = new(0.98f, 0.82f, 0.15f);
	static readonly Color SubtleGold = new(0.95f, 0.84f, 0.50f);

	// ── Boss name → dungeon + index lookup ────────────────────────────────────
	static readonly Dictionary<string, (DungeonDefinition Dungeon, int BossIndex)> BossLookup;

	static DevBossPopup()
	{
		BossLookup = new Dictionary<string, (DungeonDefinition, int)>(
			StringComparer.OrdinalIgnoreCase);

		foreach (var dungeon in DungeonDefinition.All)
			for (var i = 0; i < dungeon.BossNames.Length; i++)
				BossLookup[dungeon.BossNames[i]] = (dungeon, i);
	}

	// ── main panel fields ─────────────────────────────────────────────────────
	LineEdit _input = null!;
	Label _matchLabel = null!;
	Button _launchBtn = null!;
	Label _loadoutStatusLabel = null!;
	(DungeonDefinition Dungeon, int BossIndex)? _currentMatch;

	// ── sub-panel nodes ───────────────────────────────────────────────────────
	TalentSelector _talentSelector = null!;
	CanvasLayer _spellPanel = null!;
	CanvasLayer _itemPanel = null!;

	// ── dev spell state ───────────────────────────────────────────────────────
	/// <summary>Local mirror of the spell loadout; written through to RunState on every toggle.</summary>
	readonly SpellResource?[] _devLoadout = new SpellResource?[Player.MaxSpellSlots];

	// Visual references populated by BuildDevSpellPanel; used by RefreshDevSpellVisuals.
	readonly Dictionary<string, StyleBoxFlat> _spellCardBorders = new();
	(StyleBoxFlat Border, TextureRect Icon)[]? _slotVisuals;
	(StyleBoxFlat Border, TextureRect Icon)? _ultSlotVisual;

	// ── dev item state ────────────────────────────────────────────────────────
	EquipmentPane? _equipmentPane;
	VBoxContainer? _itemPickerContent;

	// ══════════════════════════════════════════════════════════════════════════
	// LIFECYCLE
	// ══════════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		if (!OS.IsDebugBuild()) return; // no-op in release

		Layer = 50;
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;

		// ── Dimmer ────────────────────────────────────────────────────────────
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.72f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(dimmer);

		// ── Main panel ────────────────────────────────────────────────────────
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = DevAccent;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 28f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 22f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;
		panel.CustomMinimumSize = new Vector2(520f, 0f);
		AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// ── Title row ─────────────────────────────────────────────────────────
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		vbox.AddChild(titleRow);

		var titleLabel = new Label();
		titleLabel.Text = "⚙  Dev Boss Test";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleRow.AddChild(titleLabel);

		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.Flat = true;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		closeBtn.Pressed += () =>
		{
			CloseDevSubPanels();
			Visible = false;
		};
		titleRow.AddChild(closeBtn);

		// ── Separator ─────────────────────────────────────────────────────────
		AddDevSep(vbox);

		// ── Boss search ───────────────────────────────────────────────────────
		var inputLabel = new Label();
		inputLabel.Text = "Boss Name  (exact match, case-insensitive)";
		inputLabel.AddThemeFontSizeOverride("font_size", 12);
		inputLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.52f, 0.48f));
		vbox.AddChild(inputLabel);

		_input = new LineEdit();
		_input.PlaceholderText = "e.g. Crystal Knight";
		_input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_input.AddThemeFontSizeOverride("font_size", 16);
		_input.TextChanged += OnInputChanged;
		vbox.AddChild(_input);

		_matchLabel = new Label();
		_matchLabel.Text = "";
		_matchLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_matchLabel.AddThemeFontSizeOverride("font_size", 13);
		_matchLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));
		vbox.AddChild(_matchLabel);

		_launchBtn = MakeLaunchButton();
		_launchBtn.Visible = false;
		vbox.AddChild(_launchBtn);

		// ── Dev Loadout section ───────────────────────────────────────────────
		AddDevSep(vbox);
		BuildDevLoadoutSection(vbox);

		// ── Bottom hint ───────────────────────────────────────────────────────
		var hint = new Label();
		hint.Text = "Ctrl+Alt+O to toggle  •  Esc to close";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		// ── Sub-panels (all at Layer 51, above this popup at 50) ─────────────
		_talentSelector = new TalentSelector();
		AddChild(_talentSelector);
		_talentSelector.Layer = 51;

		_spellPanel = BuildDevSpellPanel();
		AddChild(_spellPanel);

		_itemPanel = BuildDevItemPanel();
		AddChild(_itemPanel);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (!OS.IsDebugBuild()) return;
		if (ev is not InputEventKey kb || !kb.Pressed || kb.Echo) return;

		// Toggle on Ctrl+Alt+O
		if (kb.Keycode == Key.O && kb.CtrlPressed && kb.AltPressed)
		{
			Visible = !Visible;
			if (Visible)
			{
				// Sync dev loadout mirror from RunState when opening
				Array.Copy(RunState.Instance.SelectedSpells, _devLoadout, Player.MaxSpellSlots);
				_input.Clear();
				OnInputChanged("");
				_input.GrabFocus();
				RefreshLoadoutStatus();
			}
			else
			{
				CloseDevSubPanels();
			}

			GetViewport().SetInputAsHandled();
			return;
		}

		// Escape: close sub-panels first, then the main popup
		if (kb.Keycode == Key.Escape && Visible)
		{
			if (_spellPanel.Visible)
			{
				_spellPanel.Visible = false;
				RefreshLoadoutStatus();
			}
			else if (_itemPanel.Visible)
			{
				_itemPanel.Visible = false;
				RefreshLoadoutStatus();
			}
			else if (!_talentSelector.IsOpen) // TalentSelector handles its own Escape
			{
				CloseDevSubPanels();
				Visible = false;
			}

			GetViewport().SetInputAsHandled();
		}
	}

	// ══════════════════════════════════════════════════════════════════════════
	// MAIN POPUP — event handlers
	// ══════════════════════════════════════════════════════════════════════════

	void OnInputChanged(string text)
	{
		var trimmed = text.Trim();

		if (BossLookup.TryGetValue(trimmed, out var match))
		{
			_currentMatch = match;
			_matchLabel.Text = $"✓  {match.Dungeon.Name}  —  Boss {match.BossIndex + 1} of {match.Dungeon.BossCount}";
			_matchLabel.AddThemeColorOverride("font_color", new Color(0.40f, 0.82f, 0.40f));
			_launchBtn.Visible = true;
		}
		else
		{
			_currentMatch = null;
			_matchLabel.Text = string.IsNullOrWhiteSpace(trimmed) ? "" : "No matching boss found";
			_matchLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.38f, 0.35f));
			_launchBtn.Visible = false;
		}
	}

	void OnLaunchPressed()
	{
		if (_currentMatch == null) return;
		CloseDevSubPanels();
		var (dungeon, bossIndex) = _currentMatch.Value;
		RunState.Instance.SetupDevTestFight(dungeon, bossIndex);
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	// ══════════════════════════════════════════════════════════════════════════
	// DEV LOADOUT SECTION  (inside the main panel)
	// ══════════════════════════════════════════════════════════════════════════

	void BuildDevLoadoutSection(VBoxContainer parent)
	{
		var sectionLabel = new Label();
		sectionLabel.Text = "Dev Loadout";
		sectionLabel.AddThemeFontSizeOverride("font_size", 13);
		sectionLabel.AddThemeColorOverride("font_color", DevAccent);
		parent.AddChild(sectionLabel);

		var btnRow = new HBoxContainer();
		btnRow.AddThemeConstantOverride("separation", 10);
		parent.AddChild(btnRow);

		btnRow.AddChild(MakeDevButton("🎭  Talents", () => _talentSelector.OpenDev()));
		btnRow.AddChild(MakeDevButton("✨  Spells", OpenDevSpellPanel));
		btnRow.AddChild(MakeDevButton("🗡  Items", OpenDevItemPanel));

		_loadoutStatusLabel = new Label();
		_loadoutStatusLabel.Text = "";
		_loadoutStatusLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_loadoutStatusLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_loadoutStatusLabel.AddThemeFontSizeOverride("font_size", 11);
		_loadoutStatusLabel.AddThemeColorOverride("font_color", HintColor);
		parent.AddChild(_loadoutStatusLabel);
	}

	void RefreshLoadoutStatus()
	{
		if (_loadoutStatusLabel == null) return;
		var spellCount = RunState.Instance.SelectedSpells.Count(s => s != null);
		var ultSuffix = RunState.Instance.SelectedUltimate != null ? " + Ult" : "";
		var talentCount = RunState.Instance.SelectedTalentDefs.Count;
		var itemCount = ItemStore.Inventory.Count + ItemStore.Equipped.Count;
		_loadoutStatusLabel.Text =
			$"Spells: {spellCount}/6{ultSuffix}  •  Talents: {talentCount}  •  Items: {itemCount}";
	}

	void CloseDevSubPanels()
	{
		if (_talentSelector?.IsOpen == true) _talentSelector.Close();
		if (_spellPanel != null) _spellPanel.Visible = false;
		if (_itemPanel != null) _itemPanel.Visible = false;
		RefreshLoadoutStatus();
	}

	void OpenDevSpellPanel()
	{
		// Sync local mirror from RunState before showing
		Array.Copy(RunState.Instance.SelectedSpells, _devLoadout, Player.MaxSpellSlots);
		RefreshDevSpellVisuals();
		_spellPanel.Visible = true;
	}

	void OpenDevItemPanel()
	{
		RebuildDevItemPickerContent();
		_equipmentPane?.Refresh();
		_itemPanel.Visible = true;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// SPELL PANEL
	// ══════════════════════════════════════════════════════════════════════════

	CanvasLayer BuildDevSpellPanel()
	{
		var layer = new CanvasLayer { Layer = 51 };
		layer.Visible = false;

		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.6f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 60);
		margin.AddThemeConstantOverride("margin_right", 60);
		margin.AddThemeConstantOverride("margin_top", 40);
		margin.AddThemeConstantOverride("margin_bottom", 40);
		layer.AddChild(margin);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = DevAccent;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 24f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 18f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Title row
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		vbox.AddChild(titleRow);

		var title = new Label();
		title.Text = "⚙  Dev Spellbook";
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.AddThemeFontSizeOverride("font_size", 20);
		title.AddThemeColorOverride("font_color", TitleColor);
		titleRow.AddChild(title);

		var clearBtn = MakeDevButton("Clear All", () =>
		{
			Array.Clear(_devLoadout, 0, _devLoadout.Length);
			RunState.Instance.SetSpells(_devLoadout);
			RunState.Instance.SelectedUltimate = null;
			RefreshDevSpellVisuals();
			RefreshLoadoutStatus();
		});
		titleRow.AddChild(clearBtn);

		var closeBtn = MakeXCloseButton(() =>
		{
			_spellPanel.Visible = false;
			RefreshLoadoutStatus();
		});
		titleRow.AddChild(closeBtn);

		AddDevSep(vbox);

		// School columns
		var schoolScroll = new ScrollContainer();
		schoolScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		schoolScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		var schoolHbox = new HBoxContainer();
		schoolHbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		schoolHbox.AddThemeConstantOverride("separation", 0);
		schoolScroll.AddChild(schoolHbox);

		(SpellSchool School, string Name, Color Accent)[] schoolDefs =
		{
			(SpellSchool.Holy, "Holy", new Color(0.95f, 0.85f, 0.40f)),
			(SpellSchool.Nature, "Nature", new Color(0.40f, 0.80f, 0.35f)),
			(SpellSchool.Void, "Void", new Color(0.65f, 0.35f, 0.85f)),
			(SpellSchool.Chronomancy, "Chronomancy", new Color(0.35f, 0.75f, 0.90f)),
			(SpellSchool.Sanguimancy, "Sanguimancy", new Color(0.85f, 0.15f, 0.15f))
		};

		for (var i = 0; i < schoolDefs.Length; i++)
		{
			if (i > 0)
			{
				var vsep = new VSeparator();
				vsep.AddThemeColorOverride("color", DevSepColor);
				vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
				schoolHbox.AddChild(vsep);
			}

			var (school, name, accent) = schoolDefs[i];
			schoolHbox.AddChild(BuildDevSpellSchoolColumn(school, name, accent));
		}

		vbox.AddChild(schoolScroll);
		AddDevSep(vbox);

		// Loadout row
		vbox.AddChild(BuildDevSpellLoadoutRow());

		var hint = new Label();
		hint.Text = "Click to equip/unequip  •  Click an equipped slot to clear it";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return layer;
	}

	Control BuildDevSpellSchoolColumn(SpellSchool school, string colName, Color accent)
	{
		var margin = new MarginContainer();
		margin.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddThemeConstantOverride("margin_left", 14);
		margin.AddThemeConstantOverride("margin_right", 14);
		margin.AddThemeConstantOverride("margin_top", 10);
		margin.AddThemeConstantOverride("margin_bottom", 10);

		var col = new VBoxContainer();
		col.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		col.AddThemeConstantOverride("separation", 8);
		margin.AddChild(col);

		// Header
		var header = new Label();
		header.Text = colName;
		header.HorizontalAlignment = HorizontalAlignment.Center;
		header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddThemeFontSizeOverride("font_size", 14);
		header.AddThemeColorOverride("font_color", accent);
		col.AddChild(header);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(accent.R, accent.G, accent.B, 0.40f));
		col.AddChild(sep);

		// Scrollable spell cards
		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		col.AddChild(scroll);

		var flow = new HFlowContainer();
		flow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		flow.AddThemeConstantOverride("h_separation", 8);
		flow.AddThemeConstantOverride("v_separation", 8);
		scroll.AddChild(flow);

		var spells = SpellRegistry.AllSpells
			.Where(s => s.School == school)
			.ToList();

		if (spells.Count == 0)
		{
			var empty = new Label();
			empty.Text = "Coming soon";
			empty.HorizontalAlignment = HorizontalAlignment.Center;
			empty.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			empty.AddThemeFontSizeOverride("font_size", 11);
			empty.AddThemeColorOverride("font_color", HintColor);
			flow.AddChild(empty);
		}
		else
		{
			foreach (var spell in spells)
				flow.AddChild(BuildDevSpellCard(spell));
		}

		return margin;
	}

	PanelContainer BuildDevSpellCard(SpellResource spell)
	{
		var isUlt = spell is UltimateSpellResource;

		var border = new StyleBoxFlat();
		border.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.97f);
		border.SetCornerRadiusAll(5);
		border.SetBorderWidthAll(2);
		border.BorderColor = IsDevEquipped(spell)
			? isUlt ? new Color(0.65f, 0.22f, 0.90f) : GoldColor
			: new Color(0.28f, 0.22f, 0.16f);
		border.ContentMarginLeft = border.ContentMarginRight = 4f;
		border.ContentMarginTop = border.ContentMarginBottom = 4f;

		var card = new PanelContainer();
		card.CustomMinimumSize = new Vector2(72f, 88f);
		card.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		card.AddThemeStyleboxOverride("panel", border);

		_spellCardBorders[spell.Name ?? spell.GetType().Name] = border;

		var inner = new VBoxContainer();
		inner.AddThemeConstantOverride("separation", 3);
		card.AddChild(inner);

		var icon = new TextureRect();
		icon.Texture = spell.Icon;
		icon.CustomMinimumSize = new Vector2(58f, 58f);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		icon.MouseFilter = Control.MouseFilterEnum.Ignore;
		inner.AddChild(icon);

		var nameLabel = new Label();
		nameLabel.Text = spell.Name ?? "";
		nameLabel.HorizontalAlignment = HorizontalAlignment.Center;
		nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		nameLabel.AddThemeFontSizeOverride("font_size", 9);
		nameLabel.AddThemeColorOverride("font_color",
			isUlt ? new Color(0.80f, 0.55f, 0.95f) : new Color(0.88f, 0.84f, 0.78f));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		inner.AddChild(nameLabel);

		card.MouseEntered += () =>
		{
			if (!IsDevEquipped(spell))
				border.BorderColor = new Color(0.70f, 0.58f, 0.30f);
			var tt = GameTooltip.FormatSpellTooltip(spell);
			GameTooltip.Show(tt.title, tt.desc);
		};
		card.MouseExited += () =>
		{
			border.BorderColor = IsDevEquipped(spell)
				? isUlt ? new Color(0.65f, 0.22f, 0.90f) : GoldColor
				: new Color(0.28f, 0.22f, 0.16f);
			GameTooltip.Hide();
		};
		card.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				ToggleDevSpell(spell);
				card.AcceptEvent();
			}
		};

		return card;
	}

	bool IsDevEquipped(SpellResource spell)
	{
		if (spell is UltimateSpellResource)
			return RunState.Instance.SelectedUltimate?.Name == spell.Name;
		return _devLoadout.Any(s => s?.Name == spell.Name);
	}

	void ToggleDevSpell(SpellResource spell)
	{
		if (spell is UltimateSpellResource ult)
		{
			RunState.Instance.SelectedUltimate =
				RunState.Instance.SelectedUltimate?.Name == ult.Name ? null : ult;
		}
		else
		{
			var slot = Array.FindIndex(_devLoadout, s => s?.Name == spell.Name);
			if (slot >= 0)
				_devLoadout[slot] = null;
			else
			{
				var empty = Array.FindIndex(_devLoadout, s => s == null);
				if (empty >= 0) _devLoadout[empty] = spell;
			}

			RunState.Instance.SetSpells(_devLoadout);
		}

		RefreshDevSpellVisuals();
		RefreshLoadoutStatus();
	}

	void RefreshDevSpellVisuals()
	{
		var selectedUlt = RunState.Instance.SelectedUltimate;

		foreach (var (name, border) in _spellCardBorders)
		{
			var spell = SpellRegistry.AllSpells.FirstOrDefault(s => (s.Name ?? s.GetType().Name) == name);
			var isUlt = spell is UltimateSpellResource;
			var equipped = isUlt ? selectedUlt?.Name == name : _devLoadout.Any(s => s?.Name == name);
			border.BorderColor = equipped
				? isUlt ? new Color(0.65f, 0.22f, 0.90f) : GoldColor
				: new Color(0.28f, 0.22f, 0.16f);
		}

		if (_slotVisuals != null)
		{
			for (var i = 0; i < Player.MaxSpellSlots; i++)
			{
				var spell = _devLoadout[i];
				var (border, icon) = _slotVisuals[i];
				icon.Texture = spell?.Icon;
				icon.Visible = spell != null;
				border.BorderColor = spell != null
					? new Color(0.60f, 0.48f, 0.22f)
					: new Color(0.22f, 0.18f, 0.14f);
			}
		}

		if (_ultSlotVisual.HasValue)
		{
			var (border, icon) = _ultSlotVisual.Value;
			icon.Texture = selectedUlt?.Icon;
			icon.Visible = selectedUlt != null;
			border.BorderColor = selectedUlt != null
				? new Color(0.65f, 0.22f, 0.90f)
				: new Color(0.20f, 0.14f, 0.28f);
		}
	}

	Control BuildDevSpellLoadoutRow()
	{
		_slotVisuals = new (StyleBoxFlat Border, TextureRect Icon)[Player.MaxSpellSlots];

		var center = new CenterContainer();
		center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		var outerPanel = new PanelContainer();
		var outerStyle = new StyleBoxFlat();
		outerStyle.BgColor = new Color(0.08f, 0.07f, 0.05f, 0.92f);
		outerStyle.SetCornerRadiusAll(8);
		outerStyle.SetBorderWidthAll(1);
		outerStyle.BorderColor = DevSepColor;
		outerPanel.AddThemeStyleboxOverride("panel", outerStyle);
		center.AddChild(outerPanel);

		var innerMargin = new MarginContainer();
		innerMargin.AddThemeConstantOverride("margin_left", 16);
		innerMargin.AddThemeConstantOverride("margin_right", 16);
		innerMargin.AddThemeConstantOverride("margin_top", 10);
		innerMargin.AddThemeConstantOverride("margin_bottom", 10);
		outerPanel.AddChild(innerMargin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 6);
		innerMargin.AddChild(vbox);

		// Header labels
		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 0);
		vbox.AddChild(headerRow);

		var equippedLabel = new Label();
		equippedLabel.Text = "Equipped Spells";
		equippedLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		equippedLabel.AddThemeFontSizeOverride("font_size", 12);
		equippedLabel.AddThemeColorOverride("font_color", SubtleGold);
		headerRow.AddChild(equippedLabel);

		var ultLabel = new Label();
		ultLabel.Text = "Ultimate";
		ultLabel.CustomMinimumSize = new Vector2(58f, 0f);
		ultLabel.HorizontalAlignment = HorizontalAlignment.Center;
		ultLabel.AddThemeFontSizeOverride("font_size", 12);
		ultLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.50f, 0.95f));
		headerRow.AddChild(ultLabel);

		// Slot row
		var slotHbox = new HBoxContainer();
		slotHbox.AddThemeConstantOverride("separation", 8);
		vbox.AddChild(slotHbox);

		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var idx = i;
			var (slotPanel, slotBorder, slotIcon) = BuildDevLoadoutSlot(false);
			_slotVisuals[i] = (slotBorder, slotIcon);

			var spell = _devLoadout[idx];
			slotIcon.Texture = spell?.Icon;
			slotIcon.Visible = spell != null;
			slotBorder.BorderColor = spell != null ? new Color(0.60f, 0.48f, 0.22f) : new Color(0.22f, 0.18f, 0.14f);

			slotPanel.MouseEntered += () =>
			{
				var s = _devLoadout[idx];
				if (s != null)
				{
					var tt = GameTooltip.FormatSpellTooltip(s);
					GameTooltip.Show(tt.title, tt.desc);
				}
			};
			slotPanel.MouseExited += () => GameTooltip.Hide();
			slotPanel.GuiInput += ev =>
			{
				if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
				{
					_devLoadout[idx] = null;
					RunState.Instance.SetSpells(_devLoadout);
					RefreshDevSpellVisuals();
					RefreshLoadoutStatus();
					slotPanel.AcceptEvent();
				}
			};
			slotHbox.AddChild(slotPanel);
		}

		// Spacer gap before ultimate
		var gap = new Control();
		gap.CustomMinimumSize = new Vector2(16f, 0f);
		slotHbox.AddChild(gap);

		// Ultimate slot
		var (ultPanel, ultBorder, ultIcon) = BuildDevLoadoutSlot(true);
		var selUlt = RunState.Instance.SelectedUltimate;
		ultIcon.Texture = selUlt?.Icon;
		ultIcon.Visible = selUlt != null;
		ultBorder.BorderColor = selUlt != null ? new Color(0.65f, 0.22f, 0.90f) : new Color(0.20f, 0.14f, 0.28f);
		_ultSlotVisual = (ultBorder, ultIcon);

		ultPanel.MouseEntered += () =>
		{
			var u = RunState.Instance.SelectedUltimate;
			if (u != null)
			{
				var tt = GameTooltip.FormatSpellTooltip(u);
				GameTooltip.Show(tt.title, tt.desc);
			}
		};
		ultPanel.MouseExited += () => GameTooltip.Hide();
		ultPanel.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
			{
				RunState.Instance.SelectedUltimate = null;
				RefreshDevSpellVisuals();
				RefreshLoadoutStatus();
				ultPanel.AcceptEvent();
			}
		};
		slotHbox.AddChild(ultPanel);

		return center;
	}

	(PanelContainer Panel, StyleBoxFlat Border, TextureRect Icon) BuildDevLoadoutSlot(bool isUltimate)
	{
		var border = new StyleBoxFlat();
		border.BgColor = isUltimate ? new Color(0.10f, 0.07f, 0.14f, 0.95f) : new Color(0.12f, 0.10f, 0.10f, 0.95f);
		border.SetCornerRadiusAll(4);
		border.SetBorderWidthAll(2);
		border.BorderColor = isUltimate ? new Color(0.20f, 0.14f, 0.28f) : new Color(0.22f, 0.18f, 0.14f);
		border.ContentMarginLeft = border.ContentMarginRight = 3f;
		border.ContentMarginTop = border.ContentMarginBottom = 3f;

		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(58f, 58f);
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		panel.AddThemeStyleboxOverride("panel", border);

		var inner = new Control();
		inner.MouseFilter = Control.MouseFilterEnum.Ignore;
		inner.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		inner.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		panel.AddChild(inner);

		var icon = new TextureRect();
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		icon.MouseFilter = Control.MouseFilterEnum.Ignore;
		icon.Visible = false;
		inner.AddChild(icon);

		return (panel, border, icon);
	}

	// ══════════════════════════════════════════════════════════════════════════
	// ITEM PANEL
	// ══════════════════════════════════════════════════════════════════════════

	CanvasLayer BuildDevItemPanel()
	{
		var layer = new CanvasLayer { Layer = 51 };
		layer.Visible = false;

		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color = new Color(0f, 0f, 0f, 0.6f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		layer.AddChild(dimmer);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 60);
		margin.AddThemeConstantOverride("margin_right", 60);
		margin.AddThemeConstantOverride("margin_top", 40);
		margin.AddThemeConstantOverride("margin_bottom", 40);
		layer.AddChild(margin);

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = PanelBg;
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = DevAccent;
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 24f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 18f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		margin.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(vbox);

		// Title row
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		vbox.AddChild(titleRow);

		var title = new Label();
		title.Text = "⚙  Dev Items";
		title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		title.AddThemeFontSizeOverride("font_size", 20);
		title.AddThemeColorOverride("font_color", TitleColor);
		titleRow.AddChild(title);

		var clearBtn = MakeDevButton("Clear All", () =>
		{
			ItemStore.Clear();
			_equipmentPane?.Refresh();
			RebuildDevItemPickerContent();
			RefreshLoadoutStatus();
		});
		titleRow.AddChild(clearBtn);

		var closeBtn = MakeXCloseButton(() =>
		{
			_itemPanel.Visible = false;
			RefreshLoadoutStatus();
		});
		titleRow.AddChild(closeBtn);

		AddDevSep(vbox);

		// Split: left = item picker, right = equipment pane
		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 16);
		vbox.AddChild(hbox);

		// ── Left: item picker ─────────────────────────────────────────────────
		var leftVbox = new VBoxContainer();
		leftVbox.CustomMinimumSize = new Vector2(300f, 0f);
		leftVbox.AddThemeConstantOverride("separation", 8);
		hbox.AddChild(leftVbox);

		var pickerHeader = new Label();
		pickerHeader.Text = "Available Items";
		pickerHeader.AddThemeFontSizeOverride("font_size", 15);
		pickerHeader.AddThemeColorOverride("font_color", SubtleGold);
		leftVbox.AddChild(pickerHeader);

		var pickerHint = new Label();
		pickerHint.Text = "Click  Add  to put an item in your inventory,\nthen drag it to an equipment slot on the right.";
		pickerHint.AutowrapMode = TextServer.AutowrapMode.Word;
		pickerHint.AddThemeFontSizeOverride("font_size", 11);
		pickerHint.AddThemeColorOverride("font_color", HintColor);
		leftVbox.AddChild(pickerHint);

		var pickerScroll = new ScrollContainer();
		pickerScroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		pickerScroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		leftVbox.AddChild(pickerScroll);

		_itemPickerContent = new VBoxContainer();
		_itemPickerContent.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_itemPickerContent.AddThemeConstantOverride("separation", 4);
		pickerScroll.AddChild(_itemPickerContent);

		// ── Divider ───────────────────────────────────────────────────────────
		var vsep = new VSeparator();
		vsep.AddThemeColorOverride("color", DevSepColor);
		vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		hbox.AddChild(vsep);

		// ── Right: equipment pane ─────────────────────────────────────────────
		_equipmentPane = new EquipmentPane();
		_equipmentPane.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_equipmentPane.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		hbox.AddChild(_equipmentPane);

		return layer;
	}

	void RebuildDevItemPickerContent()
	{
		if (_itemPickerContent == null) return;

		foreach (var child in _itemPickerContent.GetChildren())
			child.QueueFree();

		foreach (var factory in ItemRegistry.AllItemFactories)
		{
			var sample = factory(); // temporary instance for display info
			var capturedFactory = factory;
			var alreadyHave = ItemStore.HasFoundItem(sample.ItemId);

			var row = new HBoxContainer();
			row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.AddThemeConstantOverride("separation", 10);
			row.MouseFilter = Control.MouseFilterEnum.Stop;
			_itemPickerContent.AddChild(row);

			// Tooltip — same format as EquipSlotControl
			var capturedSample = sample;
			row.MouseEntered += () => GameTooltip.Show(
				capturedSample.Name,
				$"{capturedSample.Rarity}  •  {EquipSlotControl.SlotDisplayName(capturedSample.Slot)}\n\n{capturedSample.Description}");
			row.MouseExited += () => GameTooltip.Hide();

			// Icon
			var iconRect = new TextureRect();
			iconRect.Texture = sample.Icon;
			iconRect.CustomMinimumSize = new Vector2(36f, 36f);
			iconRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			iconRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			iconRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			row.AddChild(iconRect);

			// Name + slot
			var labelCol = new VBoxContainer();
			labelCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			labelCol.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			labelCol.AddThemeConstantOverride("separation", 2);
			labelCol.MouseFilter = Control.MouseFilterEnum.Ignore;
			row.AddChild(labelCol);

			var nameLabel = new Label();
			nameLabel.Text = sample.Name;
			nameLabel.AddThemeFontSizeOverride("font_size", 12);
			nameLabel.AddThemeColorOverride("font_color",
				alreadyHave ? HintColor : new Color(0.88f, 0.84f, 0.78f));
			nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			labelCol.AddChild(nameLabel);

			var slotLabel = new Label();
			slotLabel.Text = sample.Slot.ToString();
			slotLabel.AddThemeFontSizeOverride("font_size", 10);
			slotLabel.AddThemeColorOverride("font_color", HintColor);
			slotLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			labelCol.AddChild(slotLabel);

			// Add button
			var addBtn = new Button();
			addBtn.Text = alreadyHave ? "✓" : "Add";
			addBtn.Disabled = alreadyHave;
			addBtn.CustomMinimumSize = new Vector2(52f, 32f);
			addBtn.MouseDefaultCursorShape =
				alreadyHave ? Control.CursorShape.Arrow : Control.CursorShape.PointingHand;
			addBtn.AddThemeFontSizeOverride("font_size", 11);
			addBtn.Pressed += () =>
			{
				ItemStore.AddToInventory(capturedFactory());
				_equipmentPane?.Refresh();
				RebuildDevItemPickerContent();
				RefreshLoadoutStatus();
			};
			row.AddChild(addBtn);
		}
	}

	// ══════════════════════════════════════════════════════════════════════════
	// SHARED UI HELPERS
	// ══════════════════════════════════════════════════════════════════════════

	static void AddDevSep(VBoxContainer parent)
	{
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", DevSepColor);
		parent.AddChild(sep);
	}

	Button MakeDevButton(string text, Action pressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 13);
		btn.AddThemeColorOverride("font_color", new Color(0.85f, 0.82f, 0.78f));
		btn.AddThemeColorOverride("font_hover_color", TitleColor);

		var normal = MakeButtonStyle(new Color(0.10f, 0.14f, 0.18f), new Color(0.25f, 0.55f, 0.75f));
		var hover = MakeButtonStyle(new Color(0.14f, 0.20f, 0.26f), DevAccent);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);
		btn.Pressed += pressed;
		return btn;
	}

	static Button MakeXCloseButton(Action pressed)
	{
		var btn = new Button();
		btn.Text = "✕";
		btn.Flat = true;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		btn.Pressed += pressed;
		return btn;
	}

	// ══════════════════════════════════════════════════════════════════════════
	// LAUNCH BUTTON
	// ══════════════════════════════════════════════════════════════════════════

	Button MakeLaunchButton()
	{
		var btn = new Button();
		btn.Text = "⚔  Launch Test Fight";
		btn.CustomMinimumSize = new Vector2(220f, 48f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", TitleColor);

		var normal = MakeButtonStyle(new Color(0.10f, 0.14f, 0.18f), new Color(0.28f, 0.60f, 0.80f));
		var hover = MakeButtonStyle(new Color(0.14f, 0.20f, 0.26f), new Color(0.40f, 0.80f, 1.00f));
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);
		btn.Pressed += OnLaunchPressed;
		return btn;
	}

	static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 14f;
		s.ContentMarginTop = s.ContentMarginBottom = 8f;
		return s;
	}
}
using System.Collections.Generic;
using Godot;
using healerfantasy;

/// <summary>
/// Root script for the Main Menu scene.
///
/// Builds the entire menu UI programmatically on a CanvasLayer.
///
/// Navigation:
///   Play     → res://levels/Overworld.tscn
///   Settings → inline hotkey-rebind panel
///   Exit     → quit
///
/// Hotkey rebinds are saved to user://keybinds.cfg and reloaded on startup
/// so choices persist across sessions.
/// </summary>
public partial class MainMenuUI : Node2D
{
	// ── colours ───────────────────────────────────────────────────────────────
	static readonly Color BgColor = new(0.06f, 0.05f, 0.05f);
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color SubtitleColor = new(0.50f, 0.46f, 0.40f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color PanelBg = new(0.10f, 0.08f, 0.07f, 0.98f);
	static readonly Color PanelBorder = new(0.65f, 0.52f, 0.28f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);

	static readonly Color BtnNormalBg = new(0.14f, 0.11f, 0.09f);
	static readonly Color BtnHoverBg = new(0.22f, 0.17f, 0.12f);
	static readonly Color BtnBorder = new(0.55f, 0.44f, 0.24f);
	static readonly Color BtnHoverBdr = new(0.85f, 0.70f, 0.35f);

	const string KeybindSavePath = "user://keybinds.cfg";
	const string KeybindSection = "spell_hotkeys";

	// ── rebind state ──────────────────────────────────────────────────────────
	string? _actionToRebind;
	Label? _rebindPromptLabel;
	readonly Dictionary<string, Label> _keybindLabels = new();

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// Apply any previously saved keybinds before building the UI
		LoadKeybinds();

		var canvas = new CanvasLayer();
		AddChild(canvas);

		// Full-screen dark background
		var bg = new ColorRect();
		bg.Color = BgColor;
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;
		canvas.AddChild(bg);

		// Centred content column
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 16);
		bg.AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "HEALER FANTASY";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 52);
		title.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(title);

		var sub = new Label();
		sub.Text = "A Roguelike Healing Experience";
		sub.HorizontalAlignment = HorizontalAlignment.Center;
		sub.AddThemeFontSizeOverride("font_size", 16);
		sub.AddThemeColorOverride("font_color", SubtitleColor);
		vbox.AddChild(sub);

		// Spacer
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 36);
		vbox.AddChild(spacer);

		// Menu buttons
		vbox.AddChild(MakeMenuButton("Play", OnPlayPressed));
		vbox.AddChild(MakeMenuButton("Settings", OnSettingsPressed));
		vbox.AddChild(MakeMenuButton("Exit", OnExitPressed));

		// Settings panel (hidden by default, added on top of bg)
		canvas.AddChild(BuildSettingsPanel());
	}

	// ── button factory ────────────────────────────────────────────────────────

	static Button MakeMenuButton(string text, System.Action onPressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(240f, 56f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 20);
		btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", TitleColor);

		var normal = MakeBtnStyle(BtnNormalBg, BtnBorder);
		var hover = MakeBtnStyle(BtnHoverBg, BtnHoverBdr);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);

		btn.Pressed += onPressed;
		return btn;
	}

	static StyleBoxFlat MakeBtnStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 20f;
		s.ContentMarginTop = s.ContentMarginBottom = 12f;
		return s;
	}

	// ── navigation ────────────────────────────────────────────────────────────

	void OnPlayPressed()
	{
		// Always start from a clean slate when the player hits Play
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	void OnSettingsPressed()
	{
		var panel = GetTree().Root.FindChild("SettingsPanel", true, false) as Control;
		if (panel != null)
			panel.Visible = true;
	}

	void OnExitPressed()
	{
		GetTree().Quit();
	}

	// ── settings panel ────────────────────────────────────────────────────────

	Control BuildSettingsPanel()
	{
		// Dark overlay
		var overlay = new ColorRect();
		overlay.Name = "SettingsPanel";
		overlay.Color = new Color(0f, 0f, 0f, 0.75f);
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		overlay.Visible = false;

		var panel = new PanelContainer();
		var style = new StyleBoxFlat();
		style.BgColor = PanelBg;
		style.SetCornerRadiusAll(10);
		style.SetBorderWidthAll(2);
		style.BorderColor = PanelBorder;
		style.ContentMarginLeft = style.ContentMarginRight = 32f;
		style.ContentMarginTop = style.ContentMarginBottom = 24f;
		panel.AddThemeStyleboxOverride("panel", style);
		panel.AnchorLeft = panel.AnchorRight = 0.5f;
		panel.AnchorTop = panel.AnchorBottom = 0.5f;
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;
		overlay.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 14);
		panel.AddChild(vbox);

		// Title row
		var titleRow = new HBoxContainer();
		var titleSpacer = new Control();
		titleSpacer.CustomMinimumSize = new Vector2(28, 0);
		titleRow.AddChild(titleSpacer);

		var titleLabel = new Label();
		titleLabel.Text = "Settings";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeFontSizeOverride("font_size", 22);
		titleLabel.AddThemeColorOverride("font_color", TitleColor);
		titleRow.AddChild(titleLabel);

		var closeBtn = new Button();
		closeBtn.Text = "✕";
		closeBtn.Flat = true;
		closeBtn.CustomMinimumSize = new Vector2(28, 28);
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.9f, 0.55f));
		closeBtn.Pressed += () =>
		{
			overlay.Visible = false;
			_actionToRebind = null;
			if (_rebindPromptLabel != null)
				_rebindPromptLabel.Visible = false;
		};
		titleRow.AddChild(closeBtn);
		vbox.AddChild(titleRow);

		// Separator
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		// Rebind prompt label (shared, shown when waiting for a key)
		_rebindPromptLabel = new Label();
		_rebindPromptLabel.Text = "Press any key to rebind...";
		_rebindPromptLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_rebindPromptLabel.Visible = false;
		_rebindPromptLabel.AddThemeFontSizeOverride("font_size", 13);
		_rebindPromptLabel.AddThemeColorOverride("font_color", new Color(0.90f, 0.80f, 0.30f));

		// Single grid for all keybind rows so every column aligns consistently
		var grid = new GridContainer();
		grid.Columns = 3;
		grid.AddThemeConstantOverride("h_separation", 16);
		grid.AddThemeConstantOverride("v_separation", 8);

		// ── Spell Slot Hotkeys section ────────────────────────────────────────
		AddGridSectionHeader(grid, "Spell Slot Hotkeys");

		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var slotIndex = i;
			var actionName = $"spell_{slotIndex + 1}";

			var slotLabel = new Label();
			slotLabel.Text = $"Spell Slot {slotIndex + 1}";
			slotLabel.VerticalAlignment = VerticalAlignment.Center;
			slotLabel.AddThemeFontSizeOverride("font_size", 13);
			slotLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.70f));
			grid.AddChild(slotLabel);

			var keyLabel = new Label();
			keyLabel.Text = GetKeybindLabel(actionName);
			keyLabel.CustomMinimumSize = new Vector2(60, 0);
			keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
			keyLabel.VerticalAlignment = VerticalAlignment.Center;
			keyLabel.AddThemeFontSizeOverride("font_size", 14);
			keyLabel.AddThemeColorOverride("font_color", TitleColor);
			_keybindLabels[actionName] = keyLabel;
			grid.AddChild(keyLabel);

			var rebindBtn = new Button();
			rebindBtn.Text = "Rebind";
			rebindBtn.CustomMinimumSize = new Vector2(80, 28);
			rebindBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			rebindBtn.AddThemeFontSizeOverride("font_size", 12);

			var rNormal = MakeBtnStyle(BtnNormalBg, BtnBorder);
			var rHover = MakeBtnStyle(BtnHoverBg, BtnHoverBdr);
			rebindBtn.AddThemeStyleboxOverride("normal", rNormal);
			rebindBtn.AddThemeStyleboxOverride("hover", rHover);
			rebindBtn.AddThemeStyleboxOverride("pressed", rNormal);
			rebindBtn.AddThemeStyleboxOverride("focus", rNormal);

			rebindBtn.Pressed += () =>
			{
				_actionToRebind = actionName;
				_rebindPromptLabel!.Visible = true;
			};

			grid.AddChild(rebindBtn);
		}

		// ── Spell Action Hotkeys section ──────────────────────────────────────
		AddGridSectionHeader(grid, "Spell Action Hotkeys");

		var (dispelLabel, dispelKeybindLabel, dispelBtn) = BuildKeybindRow("Dispel", "dispel");
		grid.AddChild(dispelLabel);
		grid.AddChild(dispelKeybindLabel);
		grid.AddChild(dispelBtn);

		var (deflectLabel, deflectKeybindLabel, deflectBtn) = BuildKeybindRow("Deflect", "deflect");
		grid.AddChild(deflectLabel);
		grid.AddChild(deflectKeybindLabel);
		grid.AddChild(deflectBtn);

		vbox.AddChild(grid);
		vbox.AddChild(_rebindPromptLabel);

		// Hint
		var hint = new Label();
		hint.Text = "Changes are saved automatically.";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		return overlay;
	}

	// Re-usable keybind row
	public (Label slotLabel, Label keybind, Button rebindButton) BuildKeybindRow(string label, string actionName)
	{
		var slotLabel = new Label();
		slotLabel.Text = label;
		slotLabel.VerticalAlignment = VerticalAlignment.Center;
		slotLabel.AddThemeFontSizeOverride("font_size", 13);
		slotLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.70f));

		var keyLabel = new Label();
		keyLabel.Text = GetKeybindLabel(actionName);
		keyLabel.CustomMinimumSize = new Vector2(60, 0);
		keyLabel.HorizontalAlignment = HorizontalAlignment.Center;
		keyLabel.VerticalAlignment = VerticalAlignment.Center;
		keyLabel.AddThemeFontSizeOverride("font_size", 14);
		keyLabel.AddThemeColorOverride("font_color", TitleColor);
		_keybindLabels[actionName] = keyLabel;

		var rebindBtn = new Button();
		rebindBtn.Text = "Rebind";
		rebindBtn.CustomMinimumSize = new Vector2(80, 28);
		rebindBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		rebindBtn.AddThemeFontSizeOverride("font_size", 12);

		var rNormal = MakeBtnStyle(BtnNormalBg, BtnBorder);
		var rHover = MakeBtnStyle(BtnHoverBg, BtnHoverBdr);
		rebindBtn.AddThemeStyleboxOverride("normal", rNormal);
		rebindBtn.AddThemeStyleboxOverride("hover", rHover);
		rebindBtn.AddThemeStyleboxOverride("pressed", rNormal);
		rebindBtn.AddThemeStyleboxOverride("focus", rNormal);

		rebindBtn.Pressed += () =>
		{
			_actionToRebind = actionName;
			_rebindPromptLabel!.Visible = true;
		};

		// Store references so we can update the key label after rebind
		rebindBtn.SetMeta("keyLabel", keyLabel);
		rebindBtn.SetMeta("actionName", actionName);

		return (slotLabel, keyLabel, rebindBtn);
	}


	// ── rebind input ──────────────────────────────────────────────────────────

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_actionToRebind == null) return;
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

		// Escape cancels rebind without changing anything
		if (key.PhysicalKeycode == Key.Escape)
		{
			_actionToRebind = null;
			if (_rebindPromptLabel != null)
				_rebindPromptLabel.Visible = false;
			GetViewport().SetInputAsHandled();
			return;
		}

		var actionName = _actionToRebind;
		InputMap.ActionEraseEvents(actionName);
		var newEvent = new InputEventKey();
		newEvent.PhysicalKeycode = key.PhysicalKeycode;
		InputMap.ActionAddEvent(actionName, newEvent);

		SaveKeybinds();

		if (_keybindLabels.TryGetValue(actionName, out var keyLabel))
			keyLabel.Text = GetKeybindLabel(actionName);

		_actionToRebind = null;
		if (_rebindPromptLabel != null)
			_rebindPromptLabel.Visible = false;
		GetViewport().SetInputAsHandled();
	}

	// ── keybind persistence ───────────────────────────────────────────────────

	static void SaveKeybinds()
	{
		var cfg = new ConfigFile();

		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var actionName = $"spell_{i + 1}";
			var events = InputMap.ActionGetEvents(actionName);
			if (events.Count > 0 && events[0] is InputEventKey k)
				cfg.SetValue(KeybindSection, actionName, (int)k.PhysicalKeycode);
		}

		var genericActions = new[] { "deflect", "dispel" };
		foreach (var actionName in genericActions)
		{
			var events = InputMap.ActionGetEvents(actionName);
			if (events.Count > 0 && events[0] is InputEventKey k)
				cfg.SetValue(KeybindSection, actionName, (int)k.PhysicalKeycode);
		}

		cfg.Save(KeybindSavePath);
	}

	static void LoadKeybinds()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(KeybindSavePath) != Error.Ok) return;

		// Spell Slot Keybinds
		for (var i = 0; i < Player.MaxSpellSlots; i++)
		{
			var actionName = $"spell_{i + 1}";
			if (!cfg.HasSectionKey(KeybindSection, actionName)) continue;

			var keycode = (Key)(int)cfg.GetValue(KeybindSection, actionName);
			InputMap.ActionEraseEvents(actionName);
			var ev = new InputEventKey();
			ev.PhysicalKeycode = keycode;
			InputMap.ActionAddEvent(actionName, ev);
		}

		// Generic spell keybinds
		var genericActions = new[] { "deflect", "dispel" };
		foreach (var actionName in genericActions)
		{
			if (!cfg.HasSectionKey(KeybindSection, actionName)) continue;

			var keycode = (Key)(int)cfg.GetValue(KeybindSection, actionName);
			InputMap.ActionEraseEvents(actionName);
			var ev = new InputEventKey();
			ev.PhysicalKeycode = keycode;
			InputMap.ActionAddEvent(actionName, ev);
		}
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	/// <summary>
	/// Adds a section title row that visually spans all three columns of the
	/// shared keybind grid. Two empty Controls fill the unused columns so the
	/// GridContainer keeps the correct child count.
	/// </summary>
	static void AddGridSectionHeader(GridContainer grid, string text)
	{
		var header = new Label();
		header.Text = text;
		header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddThemeFontSizeOverride("font_size", 14);
		header.AddThemeColorOverride("font_color", new Color(0.75f, 0.70f, 0.60f));
		grid.AddChild(header);
		grid.AddChild(new Control()); // key-label column placeholder
		grid.AddChild(new Control()); // button column placeholder
	}

	static string GetKeybindLabel(string actionName)
	{
		var events = InputMap.ActionGetEvents(actionName);
		if (events.Count > 0 && events[0] is InputEventKey key)
		{
			if (key.PhysicalKeycode != Key.None)
				return key.PhysicalKeycode.ToString().Replace("Key", "");

			if (key.Keycode != Key.None)
				return key.Keycode.ToString();
		}

		return "Unset";
	}
}
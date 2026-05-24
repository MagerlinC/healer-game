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

	static readonly Color DangerBtnNormalBg = new(0.22f, 0.07f, 0.07f);
	static readonly Color DangerBtnHoverBg = new(0.35f, 0.10f, 0.10f);
	static readonly Color DangerBtnBorder = new(0.70f, 0.25f, 0.25f);
	static readonly Color DangerBtnHoverBdr = new(0.95f, 0.35f, 0.35f);
	static readonly Color DangerTextColor = new(0.95f, 0.55f, 0.55f);
	static readonly Color WarningTextColor = new(0.90f, 0.78f, 0.30f);

	const string KeybindSavePath = "user://keybinds.cfg";
	const string KeybindSection = "spell_hotkeys";

	const string SettingsSavePath = "user://settings.cfg";
	const string DisplaySection = "display";
	const string AudioSection = "audio";

	static readonly (string Label, int W, int H)[] Resolutions =
	{
		("1280 × 720", 1280, 720),
		("1366 × 768", 1366, 768),
		("1600 × 900", 1600, 900),
		("1920 × 1080", 1920, 1080),
		("2560 × 1440", 2560, 1440),
		("3440 × 1440 (21:9)", 3440, 1440),
		("3840 × 2160", 3840, 2160)
	};

	// ── rebind state ──────────────────────────────────────────────────────────
	string? _actionToRebind;
	Label? _rebindPromptLabel;
	OptionButton? _resolutionOptionBtn;
	OptionButton? _windowModeBtn;
	Label? _volumeValueLabel;
	readonly Dictionary<string, Label> _keybindLabels = new();

	// ── delete-save confirmation state ────────────────────────────────────────
	Control? _deleteConfirmRow;
	Button? _deleteInitialBtn;

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// Apply any previously saved keybinds and display settings before building the UI
		LoadKeybinds();
		LoadDisplaySettings();
		LoadAudioSettings();

		var canvas = new CanvasLayer();
		AddChild(canvas);

		// Full-screen dark background
		var bg = new ColorRect();
		bg.Color = BgColor;

		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.MouseFilter = Control.MouseFilterEnum.Stop;


		var bgRect = new TextureRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Texture = GD.Load<Texture2D>(AssetConstants.MainMenuPath);
		// ExpandMode.IgnoreSize lets the rect shrink/grow freely regardless of
		// the texture's native resolution, so the anchors can fill the viewport.
		// KeepAspectCovered scales the image to cover the full rect while
		// maintaining aspect ratio, centering it and trimming equally from both
		// sides — giving correct behaviour for any aspect ratio narrower than 21:9.
		bgRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		bgRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered;
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		bg.AddChild(bgRect);

		canvas.AddChild(bg);

		// ── Mist layers ───────────────────────────────────────────────────────
		var mistTex = GD.Load<Texture2D>(AssetConstants.MistTexturePath);
		var mistShader = BuildMistShader();
		AddMistLayer(bg, mistTex, mistShader, 0.015f,  -0.003f, 0.11f, 0f);
		AddMistLayer(bg, mistTex, mistShader, -0.010f,  0.002f, 0.09f, 17f);
		AddMistLayer(bg, mistTex, mistShader, 0.020f,  -0.001f, 0.07f, 33f);

		// ── Title region (top ~24 % of the screen) ───────────────────────────
		var titleRegion = new CenterContainer();
		titleRegion.AnchorLeft = 0f;
		titleRegion.AnchorRight = 1f;
		titleRegion.AnchorTop = 0.10f;
		titleRegion.AnchorBottom = 0.34f;
		titleRegion.MouseFilter = Control.MouseFilterEnum.Ignore;
		bg.AddChild(titleRegion);

		var title = new Label();
		title.Text = "Keep Us Alive";
		title.Uppercase = true;
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 80);
		title.MouseFilter = Control.MouseFilterEnum.Ignore;

		// Horizontal gradient: #DEB56A (0 %) → #FBF8EB (50 %) → #DEB56A (100 %)
		// We use a canvas_item shader so the gradient is based on the label's
		// local-space X position.  node_width is updated whenever the label's
		// rect changes so the gradient always spans the exact text width.
		var gradShader = new Shader();
		gradShader.Code =
			"shader_type canvas_item;\n" +
			"uniform float node_width = 600.0;\n" +
			"varying float grad_x;\n" +
			"void vertex() { grad_x = VERTEX.x / node_width; }\n" +
			"void fragment() {\n" +
			"    vec4 tex = texture(TEXTURE, UV);\n" +
			"    float t = clamp(grad_x, 0.0, 1.0);\n" +
			"    vec3 colorA = vec3(0.87059, 0.70980, 0.41569);\n" +
			"    vec3 colorB = vec3(0.98431, 0.97255, 0.92157);\n" +
			"    float blend = 1.0 - abs(t * 2.0 - 1.0);\n" +
			"    COLOR = vec4(mix(colorA, colorB, blend), tex.a);\n" +
			"}\n";
		var titleMat = new ShaderMaterial();
		titleMat.Shader = gradShader;
		titleMat.SetShaderParameter("node_width", 700.0f);
		title.Material = titleMat;
		// Keep node_width in sync with the label's actual rendered width
		title.ItemRectChanged += () =>
		{
			if (title.Size.X > 1f)
				titleMat.SetShaderParameter("node_width", title.Size.X);
		};
		titleRegion.AddChild(title);

		// ── Menu region (20 – 48 % of the screen) ────────────────────────────
		var menuRegion = new CenterContainer();
		menuRegion.AnchorLeft = 0f;
		menuRegion.AnchorRight = 1f;
		menuRegion.AnchorTop = 0.25f;
		menuRegion.AnchorBottom = 0.53f;
		menuRegion.MouseFilter = Control.MouseFilterEnum.Ignore;
		bg.AddChild(menuRegion);

		var menuVbox = new VBoxContainer();
		menuVbox.AddThemeConstantOverride("separation", 2);
		menuVbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		menuVbox.AddChild(MakeTextMenuItem("Play", OnPlayPressed));
		menuVbox.AddChild(MakeTextMenuItem("Settings", OnSettingsPressed));
		menuVbox.AddChild(MakeTextMenuItem("Exit", OnExitPressed));
		menuRegion.AddChild(menuVbox);

		// Settings panel (hidden by default, rendered above everything else)
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

	/// <summary>
	/// Creates a text-only menu item for the main menu.
	/// Normal colour: #CAAC72.  On hover: a bullet dot appears to the left
	/// and the text brightens to #FBF7E8.
	/// The dot always occupies its layout space (SelfModulate hides it) so the
	/// row width never shifts when the cursor enters or leaves.
	/// </summary>
	Control MakeTextMenuItem(string text, System.Action onPressed)
	{
		var row = new HBoxContainer();
		row.Alignment = BoxContainer.AlignmentMode.Center;
		row.AddThemeConstantOverride("separation", 8);
		row.MouseFilter = Control.MouseFilterEnum.Stop;
		row.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		// Bullet dot — transparent until hovered, but always takes up space
		var dot = new Label();
		dot.Text = "•";
		dot.VerticalAlignment = VerticalAlignment.Center;
		dot.AddThemeFontSizeOverride("font_size", 32);
		dot.AddThemeColorOverride("font_color", new Color(0.984f, 0.969f, 0.910f)); // #FBF7E8
		dot.SelfModulate = new Color(1f, 1f, 1f, 0f); // invisible
		dot.MouseFilter = Control.MouseFilterEnum.Ignore;

		var label = new Label();
		label.Text = text;
		label.VerticalAlignment = VerticalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 34);
		label.AddThemeColorOverride("font_color", new Color(0.792f, 0.675f, 0.447f)); // #CAAC72
		label.MouseFilter = Control.MouseFilterEnum.Ignore;

		row.MouseEntered += () =>
		{
			dot.SelfModulate = Colors.White;
			label.AddThemeColorOverride("font_color", new Color(0.984f, 0.969f, 0.910f)); // #FBF7E8
		};
		row.MouseExited += () =>
		{
			dot.SelfModulate = new Color(1f, 1f, 1f, 0f);
			label.AddThemeColorOverride("font_color", new Color(0.792f, 0.675f, 0.447f)); // #CAAC72
		};
		row.GuiInput += (InputEvent evt) =>
		{
			if (evt is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
				onPressed();
		};

		row.AddChild(dot);
		row.AddChild(label);
		return row;
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

		// ── Display section ───────────────────────────────────────────────────
		var displayHeader = new Label();
		displayHeader.Text = "Display";
		displayHeader.HorizontalAlignment = HorizontalAlignment.Left;
		displayHeader.AddThemeFontSizeOverride("font_size", 14);
		displayHeader.AddThemeColorOverride("font_color", new Color(0.75f, 0.70f, 0.60f));
		vbox.AddChild(displayHeader);

		var resRow = new HBoxContainer();
		resRow.AddThemeConstantOverride("separation", 12);

		var resLabel = new Label();
		resLabel.Text = "Resolution";
		resLabel.VerticalAlignment = VerticalAlignment.Center;
		resLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		resLabel.AddThemeFontSizeOverride("font_size", 13);
		resLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.70f));
		resRow.AddChild(resLabel);

		_resolutionOptionBtn = new OptionButton();
		_resolutionOptionBtn.CustomMinimumSize = new Vector2(160, 32);
		_resolutionOptionBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_resolutionOptionBtn.AddThemeFontSizeOverride("font_size", 13);

		var currentSize = DisplayServer.WindowGetSize();
		var savedIndex = 3; // default to 1920×1080
		for (var i = 0; i < Resolutions.Length; i++)
		{
			var (label, w, h) = Resolutions[i];
			_resolutionOptionBtn.AddItem(label, i);
			if (w == currentSize.X && h == currentSize.Y)
				savedIndex = i;
		}

		_resolutionOptionBtn.Selected = savedIndex;

		_resolutionOptionBtn.ItemSelected += (index) =>
		{
			var (_, w, h) = Resolutions[index];
			ApplyResolution(new Vector2I(w, h));
			SaveDisplaySettings();
		};
		resRow.AddChild(_resolutionOptionBtn);
		vbox.AddChild(resRow);

		// ── Window Mode row ───────────────────────────────────────────────────
		var wmRow = new HBoxContainer();
		wmRow.AddThemeConstantOverride("separation", 12);

		var wmLabel = new Label();
		wmLabel.Text = "Window Mode";
		wmLabel.VerticalAlignment = VerticalAlignment.Center;
		wmLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		wmLabel.AddThemeFontSizeOverride("font_size", 13);
		wmLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.70f));
		wmRow.AddChild(wmLabel);

		_windowModeBtn = new OptionButton();
		_windowModeBtn.CustomMinimumSize = new Vector2(180, 32);
		_windowModeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		_windowModeBtn.AddThemeFontSizeOverride("font_size", 13);
		_windowModeBtn.AddItem("Windowed", 0);
		_windowModeBtn.AddItem("Windowed Fullscreen", 1);
		_windowModeBtn.AddItem("Fullscreen", 2);

		_windowModeBtn.Selected = DisplayServer.WindowGetMode() switch
		{
			DisplayServer.WindowMode.Fullscreen => 1,
			DisplayServer.WindowMode.ExclusiveFullscreen => 2,
			_ => 0
		};

		_windowModeBtn.ItemSelected += (index) =>
		{
			var mode = index switch
			{
				1 => DisplayServer.WindowMode.Fullscreen,
				2 => DisplayServer.WindowMode.ExclusiveFullscreen,
				_ => DisplayServer.WindowMode.Windowed
			};
			ApplyWindowMode(mode);
			SaveDisplaySettings();
		};
		wmRow.AddChild(_windowModeBtn);
		vbox.AddChild(wmRow);

		var displaySep = new HSeparator();
		displaySep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(displaySep);

		// ── Audio section ─────────────────────────────────────────────────────
		var audioHeader = new Label();
		audioHeader.Text = "Audio";
		audioHeader.HorizontalAlignment = HorizontalAlignment.Left;
		audioHeader.AddThemeFontSizeOverride("font_size", 14);
		audioHeader.AddThemeColorOverride("font_color", new Color(0.75f, 0.70f, 0.60f));
		vbox.AddChild(audioHeader);

		var volRow = new HBoxContainer();
		volRow.AddThemeConstantOverride("separation", 12);

		var volLabel = new Label();
		volLabel.Text = "Volume";
		volLabel.VerticalAlignment = VerticalAlignment.Center;
		volLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		volLabel.AddThemeFontSizeOverride("font_size", 13);
		volLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.76f, 0.70f));
		volRow.AddChild(volLabel);

		var currentLinear = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")));
		var currentPct = Mathf.RoundToInt(currentLinear * 100f);

		_volumeValueLabel = new Label();
		_volumeValueLabel.Text = $"{currentPct}%";
		_volumeValueLabel.CustomMinimumSize = new Vector2(40, 0);
		_volumeValueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_volumeValueLabel.VerticalAlignment = VerticalAlignment.Center;
		_volumeValueLabel.AddThemeFontSizeOverride("font_size", 13);
		_volumeValueLabel.AddThemeColorOverride("font_color", TitleColor);

		var volSlider = new HSlider();
		volSlider.MinValue = 0;
		volSlider.MaxValue = 100;
		volSlider.Step = 1;
		volSlider.Value = currentPct;
		volSlider.CustomMinimumSize = new Vector2(160, 20);
		volSlider.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		volSlider.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		volSlider.ValueChanged += (val) =>
		{
			_volumeValueLabel.Text = $"{(int)val}%";
			ApplyVolume((float)val / 100f);
			SaveAudioSettings();
		};

		volRow.AddChild(volSlider);
		volRow.AddChild(_volumeValueLabel);
		vbox.AddChild(volRow);

		var audioSep = new HSeparator();
		audioSep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(audioSep);

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

			var (slotLabel, keyLabel, rebindBtn) = BuildKeybindRow($"Spell Slot {slotIndex + 1}", actionName);
			grid.AddChild(slotLabel);
			grid.AddChild(keyLabel);
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

		var (ultimateLabel, ultimateKeybindLabel, ultimateBtn) = BuildKeybindRow("Ultimate", "ultimate");
		grid.AddChild(ultimateLabel);
		grid.AddChild(ultimateKeybindLabel);
		grid.AddChild(ultimateBtn);

		vbox.AddChild(grid);
		vbox.AddChild(_rebindPromptLabel);

		// Hint
		var hint = new Label();
		hint.Text = "Changes are saved automatically.";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		vbox.AddChild(hint);

		// ── Danger Zone ───────────────────────────────────────────────────────
		var dangerSep = new HSeparator();
		dangerSep.AddThemeColorOverride("color", new Color(0.55f, 0.20f, 0.20f, 0.60f));
		vbox.AddChild(dangerSep);

		var dangerHeader = new Label();
		dangerHeader.Text = "Save Data";
		dangerHeader.HorizontalAlignment = HorizontalAlignment.Center;
		dangerHeader.AddThemeFontSizeOverride("font_size", 14);
		dangerHeader.AddThemeColorOverride("font_color", new Color(0.75f, 0.45f, 0.45f));
		vbox.AddChild(dangerHeader);

		// Initial delete button
		_deleteInitialBtn = MakeDangerButton("Delete Save Data", OnDeleteSavePressed);
		vbox.AddChild(_deleteInitialBtn);

		// Confirmation row (hidden until the delete button is clicked)
		_deleteConfirmRow = BuildDeleteConfirmRow(overlay);
		_deleteConfirmRow.Visible = false;
		vbox.AddChild(_deleteConfirmRow);

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
			{
				// Prefer physical_keycode (layout-independent); fall back to keycode.
				var keyToSave = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
				if (keyToSave != Key.None)
					cfg.SetValue(KeybindSection, actionName, (int)keyToSave);
			}
		}

		var genericActions = new[] { "deflect", "dispel", "ultimate" };
		foreach (var actionName in genericActions)
		{
			var events = InputMap.ActionGetEvents(actionName);
			if (events.Count > 0 && events[0] is InputEventKey k)
			{
				var keyToSave = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
				if (keyToSave != Key.None)
					cfg.SetValue(KeybindSection, actionName, (int)keyToSave);
			}
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
			if (keycode == Key.None) continue; // guard against corrupted/legacy zero entries

			InputMap.ActionEraseEvents(actionName);
			var ev = new InputEventKey();
			ev.PhysicalKeycode = keycode;
			InputMap.ActionAddEvent(actionName, ev);
		}

		// Generic spell keybinds
		var genericActions = new[] { "deflect", "dispel", "ultimate" };
		foreach (var actionName in genericActions)
		{
			if (!cfg.HasSectionKey(KeybindSection, actionName)) continue;

			var keycode = (Key)(int)cfg.GetValue(KeybindSection, actionName);
			if (keycode == Key.None) continue; // guard against corrupted/legacy zero entries

			InputMap.ActionEraseEvents(actionName);
			var ev = new InputEventKey();
			ev.PhysicalKeycode = keycode;
			InputMap.ActionAddEvent(actionName, ev);
		}
	}

	// ── display settings persistence ─────────────────────────────────────────

	static void SaveDisplaySettings()
	{
		var cfg = new ConfigFile();
		cfg.Load(SettingsSavePath); // preserve existing keys (e.g. audio)

		// Only update the saved size in windowed mode so fullscreen doesn't
		// overwrite the user's preferred windowed resolution.
		var currentMode = DisplayServer.WindowGetMode();
		if (currentMode == DisplayServer.WindowMode.Windowed)
		{
			var size = DisplayServer.WindowGetSize();
			cfg.SetValue(DisplaySection, "width", size.X);
			cfg.SetValue(DisplaySection, "height", size.Y);
		}

		cfg.SetValue(DisplaySection, "window_mode", (int)currentMode);
		cfg.Save(SettingsSavePath);
	}

	static void LoadDisplaySettings()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SettingsSavePath) != Error.Ok) return;

		// Apply window mode before size so that windowed resolution isn't
		// ignored when the saved mode is Fullscreen / ExclusiveFullscreen.
		if (cfg.HasSectionKey(DisplaySection, "window_mode"))
		{
			var mode = (DisplayServer.WindowMode)(int)cfg.GetValue(DisplaySection, "window_mode");
			ApplyWindowMode(mode);
		}

		// Only restore the saved resolution in windowed mode.
		if (DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed)
		{
			if (cfg.HasSectionKey(DisplaySection, "width") &&
			    cfg.HasSectionKey(DisplaySection, "height"))
			{
				var w = (int)cfg.GetValue(DisplaySection, "width");
				var h = (int)cfg.GetValue(DisplaySection, "height");
				if (w > 0 && h > 0)
					ApplyResolution(new Vector2I(w, h));
			}
		}
	}

	// ── audio settings persistence ────────────────────────────────────────────

	static void SaveAudioSettings()
	{
		var cfg = new ConfigFile();
		cfg.Load(SettingsSavePath); // preserve existing keys (e.g. display)
		var linear = Mathf.DbToLinear(AudioServer.GetBusVolumeDb(AudioServer.GetBusIndex("Master")));
		cfg.SetValue(AudioSection, "master_volume", Mathf.Clamp(linear, 0f, 1f));
		cfg.Save(SettingsSavePath);
	}

	static void LoadAudioSettings()
	{
		var cfg = new ConfigFile();
		if (cfg.Load(SettingsSavePath) != Error.Ok) return;
		if (!cfg.HasSectionKey(AudioSection, "master_volume")) return;

		var linear = (float)cfg.GetValue(AudioSection, "master_volume");
		ApplyVolume(Mathf.Clamp(linear, 0f, 1f));
	}

	static void ApplyVolume(float linear)
	{
		var db = linear > 0f ? Mathf.LinearToDb(linear) : -80f;
		AudioServer.SetBusVolumeDb(AudioServer.GetBusIndex("Master"), db);
	}

	static void ApplyResolution(Vector2I size)
	{
		DisplayServer.WindowSetSize(size);

		// Re-centre the window on the screen so it doesn't drift off-screen
		var screenSize = DisplayServer.ScreenGetSize();
		var centred = (screenSize - size) / 2;
		if (centred.X >= 0 && centred.Y >= 0)
			DisplayServer.WindowSetPosition(centred);
	}

	static void ApplyWindowMode(DisplayServer.WindowMode mode)
	{
		DisplayServer.WindowSetMode(mode);
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

	// ── danger-zone helpers ───────────────────────────────────────────────────

	Button MakeDangerButton(string text, System.Action onPressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(240f, 44f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 15);
		btn.AddThemeColorOverride("font_color", DangerTextColor);
		btn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.75f, 0.75f));

		var normal = MakeBtnStyle(DangerBtnNormalBg, DangerBtnBorder);
		var hover = MakeBtnStyle(DangerBtnHoverBg, DangerBtnHoverBdr);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);

		btn.Pressed += onPressed;
		return btn;
	}

	Control BuildDeleteConfirmRow(Control overlay)
	{
		var container = new VBoxContainer();
		container.AddThemeConstantOverride("separation", 8);

		var warningLabel = new Label();
		warningLabel.Text =
			"This will permanently delete all progress, run history,\nand saved loadout preferences. This cannot be undone.";
		warningLabel.HorizontalAlignment = HorizontalAlignment.Center;
		warningLabel.AddThemeFontSizeOverride("font_size", 12);
		warningLabel.AddThemeColorOverride("font_color", WarningTextColor);
		container.AddChild(warningLabel);

		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 12);

		var confirmBtn = MakeDangerButton("Yes, Delete Everything", () => OnConfirmDeletePressed(overlay));
		confirmBtn.CustomMinimumSize = new Vector2(200f, 40f);
		btnRow.AddChild(confirmBtn);

		var cancelBtn = MakeMenuButton("Cancel", OnCancelDeletePressed);
		cancelBtn.CustomMinimumSize = new Vector2(100f, 40f);
		btnRow.AddChild(cancelBtn);

		container.AddChild(btnRow);
		return container;
	}

	void OnDeleteSavePressed()
	{
		_deleteInitialBtn!.Visible = false;
		_deleteConfirmRow!.Visible = true;
	}

	void OnCancelDeletePressed()
	{
		_deleteConfirmRow!.Visible = false;
		_deleteInitialBtn!.Visible = true;
	}

	void OnConfirmDeletePressed(Control overlay)
	{
		PlayerProgressStore.DeleteSaveFile();
		RunHistoryStore.DeleteSaveFile();
		LoadoutPreferences.DeleteSaveFile();

		// Close the settings panel and reset confirmation UI state
		overlay.Visible = false;
		_deleteConfirmRow!.Visible = false;
		_deleteInitialBtn!.Visible = true;
	}

	// ── mist helpers ──────────────────────────────────────────────────────────

	/// <summary>
	/// Builds the shared canvas_item shader used by all mist layers.
	///
	/// Technique — eliminates square tiling seams and creates organic cloud shapes:
	///
	///   Detail sample A  — tiled UV, fades to 0 near its own tile edges.
	///   Detail sample B  — same tile count but offset +0.5 in both axes, so its
	///                      seams never align with A's.  Also fades near its edges.
	///   max(A, B)        — wherever A is at a seam (dark), B is in its bright
	///                      centre, and vice-versa.  The result has no hard edges.
	///   Shape mask       — a third, very slow sample at ~3× zoom sculpts the mist
	///                      into organic blob outlines instead of a uniform band.
	///   Screen fades     — smooth vertical + horizontal falloff keeps mist in the
	///                      centre of the screen and away from all four edges.
	/// </summary>
	static Shader BuildMistShader()
	{
		var s = new Shader();
		s.Code = @"
shader_type canvas_item;
uniform float scroll_x    = 0.015;
uniform float scroll_y    = 0.0;
uniform float alpha_scale = 0.07;
uniform float time_offset = 0.0;

void fragment() {
    float t  = TIME + time_offset;
    vec2  uv = vec2(UV.x * 2.5 + scroll_x * t, UV.y * 1.2 + scroll_y * t);

    // ── Detail sample A (main tile) ──────────────────────────────────────
    vec2  fA  = fract(uv);
    float lumA = dot(texture(TEXTURE, fA).rgb, vec3(0.299, 0.587, 0.114));
    // Fade to 0 near every tile edge so the wrap seam is invisible
    float eA = smoothstep(0.0, 0.30, fA.x) * (1.0 - smoothstep(0.70, 1.0, fA.x))
             * smoothstep(0.0, 0.30, fA.y) * (1.0 - smoothstep(0.70, 1.0, fA.y));

    // ── Detail sample B (half-tile offset — seams staggered from A's) ───
    vec2  fB  = fract(uv + vec2(0.5, 0.5));
    float lumB = dot(texture(TEXTURE, fB).rgb, vec3(0.299, 0.587, 0.114));
    float eB = smoothstep(0.0, 0.30, fB.x) * (1.0 - smoothstep(0.70, 1.0, fB.x))
             * smoothstep(0.0, 0.30, fB.y) * (1.0 - smoothstep(0.70, 1.0, fB.y));

    // ── Cloud-shape mask (slow, large-scale sample) ──────────────────────
    // Scrolls at 20 % of the detail speed so the cloud outlines evolve
    // independently, giving organic varying density rather than a uniform band.
    vec2  fS    = fract(vec2(UV.x * 0.85 + scroll_x * 0.2 * t,
                             UV.y * 0.55 + scroll_y * 0.2 * t));
    float shape = smoothstep(0.18, 0.55,
                             dot(texture(TEXTURE, fS).rgb, vec3(0.299, 0.587, 0.114)));

    float mist = max(lumA * eA, lumB * eB) * shape;

    // ── Screen-space soft fades ──────────────────────────────────────────
    float vm = smoothstep(0.20, 0.42, UV.y) * (1.0 - smoothstep(0.68, 0.90, UV.y));
    float hm = smoothstep(0.0,  0.12, UV.x) * (1.0 - smoothstep(0.88, 1.0,  UV.x));

    COLOR = vec4(0.72, 0.79, 0.88, mist * alpha_scale * vm * hm);
}
";
		return s;
	}

	/// <summary>
	/// Adds a single full-screen mist TextureRect with its own ShaderMaterial
	/// so it scrolls independently from the other layers.
	/// </summary>
	static void AddMistLayer(
		Control parent, Texture2D tex, Shader shader,
		float scrollX, float scrollY, float alpha, float timeOffset)
	{
		var mat = new ShaderMaterial();
		mat.Shader = shader;
		mat.SetShaderParameter("scroll_x", scrollX);
		mat.SetShaderParameter("scroll_y", scrollY);
		mat.SetShaderParameter("alpha_scale", alpha);
		mat.SetShaderParameter("time_offset", timeOffset);

		var rect = new TextureRect();
		rect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		rect.Texture = tex;
		rect.StretchMode = TextureRect.StretchModeEnum.Scale;
		rect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		rect.MouseFilter = Control.MouseFilterEnum.Ignore;
		rect.Material = mat;
		parent.AddChild(rect);
	}
}
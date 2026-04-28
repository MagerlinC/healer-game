#nullable enable
using System.Collections.Generic;
using Godot;
using healerfantasy;

/// <summary>
/// Hidden developer popup for launching test boss fights directly from the Overworld.
///
/// Toggle open / closed with Ctrl+Alt+O (also closeable with Escape).
///
/// Type a boss name in the text box. When the input matches a known boss name
/// exactly (case-insensitive), a "Launch Test Fight" button appears. Pressing it
/// configures <see cref="RunState"/> for a one-off test encounter and loads
/// World.tscn with the current spell/talent loadout.
///
/// After the test fight (win or loss) the game returns to Overworld and resets
/// the run state — no half-finished run is left behind.
/// </summary>
public partial class DevBossPopup : CanvasLayer
{
	// ── Boss name → dungeon + index lookup ────────────────────────────────────

	/// <summary>
	/// Static lookup built once from <see cref="DungeonDefinition.All"/>.
	/// Keys are boss display names; comparison is case-insensitive.
	/// </summary>
	static readonly Dictionary<string, (DungeonDefinition Dungeon, int BossIndex)> BossLookup;

	static DevBossPopup()
	{
		BossLookup = new Dictionary<string, (DungeonDefinition, int)>(
			System.StringComparer.OrdinalIgnoreCase);

		foreach (var dungeon in DungeonDefinition.All)
			for (var i = 0; i < dungeon.BossNames.Length; i++)
				BossLookup[dungeon.BossNames[i]] = (dungeon, i);
	}

	// ── private fields ────────────────────────────────────────────────────────

	LineEdit _input    = null!;
	Label    _matchLabel = null!;
	Button   _launchBtn  = null!;

	(DungeonDefinition Dungeon, int BossIndex)? _currentMatch;

	// ── Godot lifecycle ───────────────────────────────────────────────────────

	public override void _Ready()
	{
		Layer       = 50;   // above VictoryScreen (20) and all other overlays
		Visible     = false;
		ProcessMode = ProcessModeEnum.Always;

		// ── Dark dimmer — blocks all mouse events from reaching the scene ────
		var dimmer = new ColorRect();
		dimmer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		dimmer.Color       = new Color(0f, 0f, 0f, 0.72f);
		dimmer.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(dimmer);

		// ── Panel ────────────────────────────────────────────────────────────
		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.07f, 0.06f, 0.06f, 0.97f);
		panelStyle.SetCornerRadiusAll(8);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor           = new Color(0.35f, 0.75f, 0.90f); // cyan dev accent
		panelStyle.ContentMarginLeft     = panelStyle.ContentMarginRight  = 28f;
		panelStyle.ContentMarginTop      = panelStyle.ContentMarginBottom = 22f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal      = Control.GrowDirection.Both;
		panel.GrowVertical        = Control.GrowDirection.Both;
		panel.CustomMinimumSize   = new Vector2(460f, 0f);
		AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 16);
		panel.AddChild(vbox);

		// ── Title row ────────────────────────────────────────────────────────
		var titleRow = new HBoxContainer();
		titleRow.AddThemeConstantOverride("separation", 10);
		vbox.AddChild(titleRow);

		var titleLabel = new Label();
		titleLabel.Text                = "⚙  Dev Boss Test";
		titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		titleLabel.AddThemeColorOverride("font_color", new Color(0.35f, 0.85f, 1.0f));
		titleRow.AddChild(titleLabel);

		var closeBtn = new Button();
		closeBtn.Text                   = "✕";
		closeBtn.Flat                   = true;
		closeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		closeBtn.AddThemeFontSizeOverride("font_size", 16);
		closeBtn.AddThemeColorOverride("font_color",       new Color(0.72f, 0.68f, 0.62f));
		closeBtn.AddThemeColorOverride("font_hover_color", new Color(0.90f, 0.35f, 0.28f));
		closeBtn.Pressed += () => Visible = false;
		titleRow.AddChild(closeBtn);

		// ── Separator ────────────────────────────────────────────────────────
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.35f, 0.75f, 0.90f, 0.40f));
		vbox.AddChild(sep);

		// ── Input label ──────────────────────────────────────────────────────
		var inputLabel = new Label();
		inputLabel.Text = "Boss Name  (exact match, case-insensitive)";
		inputLabel.AddThemeFontSizeOverride("font_size", 12);
		inputLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.52f, 0.48f));
		vbox.AddChild(inputLabel);

		// ── Text input ───────────────────────────────────────────────────────
		_input = new LineEdit();
		_input.PlaceholderText     = "e.g. Crystal Knight";
		_input.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_input.AddThemeFontSizeOverride("font_size", 16);
		_input.TextChanged         += OnInputChanged;
		vbox.AddChild(_input);

		// ── Match feedback ────────────────────────────────────────────────────
		_matchLabel = new Label();
		_matchLabel.Text                = "";
		_matchLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_matchLabel.AddThemeFontSizeOverride("font_size", 13);
		_matchLabel.AddThemeColorOverride("font_color", new Color(0.50f, 0.50f, 0.50f));
		vbox.AddChild(_matchLabel);

		// ── Launch button ────────────────────────────────────────────────────
		_launchBtn         = MakeLaunchButton();
		_launchBtn.Visible = false;
		vbox.AddChild(_launchBtn);

		// ── Bottom hint ───────────────────────────────────────────────────────
		var hint = new Label();
		hint.Text                = "Ctrl+Alt+O to toggle  •  Esc to close";
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", new Color(0.35f, 0.35f, 0.35f));
		vbox.AddChild(hint);
	}

	public override void _UnhandledInput(InputEvent ev)
	{
		if (ev is not InputEventKey kb || !kb.Pressed || kb.Echo) return;

		// Toggle on Ctrl+Alt+O
		if (kb.Keycode == Key.O && kb.CtrlPressed && kb.AltPressed)
		{
			Visible = !Visible;
			if (Visible)
			{
				_input.Clear();
				OnInputChanged("");
				_input.GrabFocus();
			}
			GetViewport().SetInputAsHandled();
			return;
		}

		// Close on Escape
		if (Visible && kb.Keycode == Key.Escape)
		{
			Visible = false;
			GetViewport().SetInputAsHandled();
		}
	}

	// ── event handlers ────────────────────────────────────────────────────────

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
			_currentMatch      = null;
			_matchLabel.Text   = string.IsNullOrWhiteSpace(trimmed) ? "" : "No matching boss found";
			_matchLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.38f, 0.35f));
			_launchBtn.Visible = false;
		}
	}

	void OnLaunchPressed()
	{
		if (_currentMatch == null) return;

		var (dungeon, bossIndex) = _currentMatch.Value;
		RunState.Instance.SetupDevTestFight(dungeon, bossIndex);
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	// ── button builder ────────────────────────────────────────────────────────

	Button MakeLaunchButton()
	{
		var btn = new Button();
		btn.Text                   = "⚔  Launch Test Fight";
		btn.CustomMinimumSize      = new Vector2(220f, 48f);
		btn.SizeFlagsHorizontal    = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 16);
		btn.AddThemeColorOverride("font_color",       new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.35f, 0.85f, 1.0f));

		var normal = MakeButtonStyle(new Color(0.10f, 0.14f, 0.18f), new Color(0.28f, 0.60f, 0.80f));
		var hover  = MakeButtonStyle(new Color(0.14f, 0.20f, 0.26f), new Color(0.40f, 0.80f, 1.00f));
		btn.AddThemeStyleboxOverride("normal",  normal);
		btn.AddThemeStyleboxOverride("hover",   hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus",   normal);
		btn.Pressed += OnLaunchPressed;
		return btn;
	}

	static StyleBoxFlat MakeButtonStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor            = border;
		s.ContentMarginLeft      = s.ContentMarginRight  = 16f;
		s.ContentMarginTop       = s.ContentMarginBottom = 10f;
		return s;
	}
}

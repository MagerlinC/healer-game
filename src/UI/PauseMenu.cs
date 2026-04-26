using System;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;

namespace healerfantasy.UI;

/// <summary>
/// Pause menu overlay shown when the player presses Escape during a boss fight.
///
/// Sits on CanvasLayer 25 (above DeathScreen at layer 20).
/// ProcessMode is Always so input and buttons continue to work while the
/// scene tree is paused.
///
/// Pressing Escape toggles the menu open/closed.
/// The SpellbookSelector and TalentSelector already consume Escape (via
/// SetInputAsHandled) when they are open, so the pause menu only activates
/// when neither of those panels is visible.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
	bool _isOpen;

	public override void _Ready()
	{
		Layer = 25;
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;

		// ── Dark semi-transparent backdrop ────────────────────────────────────
		var overlay = new ColorRect();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0f, 0f, 0f, 0.70f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// ── Centred panel ─────────────────────────────────────────────────────
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.Center);
		panel.GrowHorizontal = Control.GrowDirection.Both;
		panel.GrowVertical = Control.GrowDirection.Both;

		var panelStyle = new StyleBoxFlat();
		panelStyle.BgColor = new Color(0.10f, 0.08f, 0.07f);
		panelStyle.SetCornerRadiusAll(10);
		panelStyle.SetBorderWidthAll(2);
		panelStyle.BorderColor = new Color(0.45f, 0.35f, 0.25f);
		panelStyle.ContentMarginLeft = panelStyle.ContentMarginRight = 48f;
		panelStyle.ContentMarginTop = panelStyle.ContentMarginBottom = 40f;
		panel.AddThemeStyleboxOverride("panel", panelStyle);
		overlay.AddChild(panel);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 18);
		panel.AddChild(vbox);

		// ── Title ─────────────────────────────────────────────────────────────
		var title = new Label();
		title.Text = "PAUSED";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 32);
		title.AddThemeColorOverride("font_color", new Color(0.92f, 0.88f, 0.80f));
		vbox.AddChild(title);

		// ── Divider ───────────────────────────────────────────────────────────
		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", new Color(0.45f, 0.35f, 0.25f, 0.60f));
		sep.CustomMinimumSize = new Vector2(280f, 0f);
		vbox.AddChild(sep);

		// ── Buttons ───────────────────────────────────────────────────────────
		vbox.AddChild(MakeButton("Resume", new Color(0.25f, 0.40f, 0.25f), OnResumePressed));
		vbox.AddChild(MakeButton("Abandon Run", new Color(0.50f, 0.20f, 0.20f), OnAbandonPressed));
		vbox.AddChild(MakeButton("Main Menu", new Color(0.35f, 0.30f, 0.22f), OnMainMenuPressed));
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

		if (key.PhysicalKeycode == Key.Escape)
		{
			if (_isOpen) Close();
			else Open();
			GetViewport().SetInputAsHandled();
		}
	}

	// ── open / close ──────────────────────────────────────────────────────────

	void Open()
	{
		_isOpen = true;
		Visible = true;
		GetTree().Paused = true;
	}

	void Close()
	{
		_isOpen = false;
		Visible = false;
		GetTree().Paused = false;
	}

	// ── button callbacks ──────────────────────────────────────────────────────

	void OnResumePressed()
	{
		Close();
	}

	void OnAbandonPressed()
	{
		CombatLog.CombatLog.Clear();
		RunHistoryStore.FinalizeRun(false);

		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/Overworld.tscn");
	}

	void OnMainMenuPressed()
	{
		CombatLog.CombatLog.Clear();
		RunHistoryStore.FinalizeRun(false);

		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static Button MakeButton(string text, Color borderColor, Action onPressed)
	{
		var btn = new Button();
		btn.Text = text;
		btn.CustomMinimumSize = new Vector2(280f, 52f);
		btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

		var normal = MakeStyle(new Color(0.16f, 0.11f, 0.11f), borderColor);
		var hover = MakeStyle(new Color(0.26f, 0.16f, 0.14f), borderColor * 1.4f);
		btn.AddThemeStyleboxOverride("normal", normal);
		btn.AddThemeStyleboxOverride("hover", hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus", normal);

		btn.Pressed += onPressed;
		return btn;
	}

	static StyleBoxFlat MakeStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor = border;
		s.ContentMarginLeft = s.ContentMarginRight = 16f;
		s.ContentMarginTop = s.ContentMarginBottom = 10f;
		return s;
	}
}
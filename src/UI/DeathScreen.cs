using System;
using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;

/// <summary>
/// Full-screen death overlay shown when every party member has died.
///
/// Sits on CanvasLayer 20 (above GameUI at layer 10) and is hidden by default.
/// <see cref="ShowDeathScreen"/> makes it visible and pauses the scene tree so
/// all game logic freezes while the overlay is up.
///
/// ProcessMode is Always so the overlay and its button keep receiving input
/// even while the tree is paused.
/// </summary>
public partial class DeathScreen : CanvasLayer
{
	readonly HashSet<string> _deadPartyMembers = new();

	void OnCharacterDied(Character character)
	{
		if (character.IsFriendly)
		{
			_deadPartyMembers.Add(character.CharacterName);
		}

		// Check if all party members are dead.  If so, show the death screen.
		if (_deadPartyMembers.Count >= 4)
		{
			ShowDeathScreen();
		}
	}
	public override void _Ready()
	{
		Layer = 20;
		Visible = false;
		ProcessMode = ProcessModeEnum.Always;

		GlobalAutoLoad.SubscribeToSignal(nameof(Character.Died), Callable.From((Character character) => OnCharacterDied(character)));

		// ── Dark overlay — blocks all mouse events from reaching the game ────
		var overlay = new ColorRect();
		overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		overlay.Color = new Color(0f, 0f, 0f, 0.80f);
		overlay.MouseFilter = Control.MouseFilterEnum.Stop;
		AddChild(overlay);

		// ── Centred content column ────────────────────────────────────────────
		var vbox = new VBoxContainer();
		vbox.SetAnchorsPreset(Control.LayoutPreset.Center);
		vbox.GrowHorizontal = Control.GrowDirection.Both;
		vbox.GrowVertical = Control.GrowDirection.Both;
		vbox.AddThemeConstantOverride("separation", 32);
		overlay.AddChild(vbox);

		// Title
		var title = new Label();
		title.Text = "YOUR PARTY HAS FALLEN";
		title.HorizontalAlignment = HorizontalAlignment.Center;
		title.AddThemeFontSizeOverride("font_size", 36);
		title.AddThemeColorOverride("font_color", new Color(0.85f, 0.22f, 0.22f));
		vbox.AddChild(title);

		// ── Button row ────────────────────────────────────────────────────────
		var btnRow = new HBoxContainer();
		btnRow.Alignment = BoxContainer.AlignmentMode.Center;
		btnRow.AddThemeConstantOverride("separation", 20);
		vbox.AddChild(btnRow);

		btnRow.AddChild(MakeButton("Retry",       new Color(0.50f, 0.20f, 0.20f), OnRetryPressed));
		btnRow.AddChild(MakeButton("Main Menu",   new Color(0.35f, 0.30f, 0.22f), OnMainMenuPressed));
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Makes the overlay visible and pauses all game logic.
	/// Safe to call multiple times (subsequent calls are no-ops).
	/// </summary>
	public void ShowDeathScreen()
	{
		if (Visible) return;

		// Record the failed boss encounter and close out the run as a loss.
		RunHistoryStore.RecordBossEncounter(
			RunState.Instance?.CurrentBossName ?? "Unknown");
		CombatLog.Clear();
		RunHistoryStore.FinalizeRun(false);

		Visible = true;
		GetTree().Paused = true;
	}

	// ── private ───────────────────────────────────────────────────────────────

	/// <summary>Restart the same boss fight with the same RunState loadout.</summary>
	void OnRetryPressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunHistoryStore.StartRun(); // Begin tracking the retry as a new run
		GetTree().ChangeSceneToFile("res://levels/World.tscn");
	}

	/// <summary>Return to Main Menu and reset RunState for a fresh run.</summary>
	void OnMainMenuPressed()
	{
		GetTree().Paused = false;
		GlobalAutoLoad.Reset();
		RunState.Instance?.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static Button MakeButton(string text, Color borderColor, System.Action onPressed)
	{
		var btn = new Button();
		btn.Text                    = text;
		btn.CustomMinimumSize       = new Vector2(180f, 52f);
		btn.SizeFlagsHorizontal     = Control.SizeFlags.ShrinkCenter;
		btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 18);
		btn.AddThemeColorOverride("font_color",       new Color(0.90f, 0.87f, 0.83f));
		btn.AddThemeColorOverride("font_hover_color", new Color(0.95f, 0.84f, 0.50f));

		var normal = MakeStyle(new Color(0.16f, 0.11f, 0.11f), borderColor);
		var hover  = MakeStyle(new Color(0.26f, 0.16f, 0.14f), borderColor * 1.4f);
		btn.AddThemeStyleboxOverride("normal",  normal);
		btn.AddThemeStyleboxOverride("hover",   hover);
		btn.AddThemeStyleboxOverride("pressed", normal);
		btn.AddThemeStyleboxOverride("focus",   normal);

		btn.Pressed += onPressed;
		return btn;
	}

	static StyleBoxFlat MakeStyle(Color bg, Color border)
	{
		var s = new StyleBoxFlat();
		s.BgColor = bg;
		s.SetCornerRadiusAll(6);
		s.SetBorderWidthAll(2);
		s.BorderColor          = border;
		s.ContentMarginLeft    = s.ContentMarginRight  = 16f;
		s.ContentMarginTop     = s.ContentMarginBottom = 10f;
		return s;
	}
}
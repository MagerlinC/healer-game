using System.Collections.Generic;
using Godot;
using healerfantasy;

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

	void OnCharacterDied(string characterName)
	{
		_deadPartyMembers.Add(characterName);

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

		GlobalAutoLoad.SubscribeToSignal(nameof(Character.Died), Callable.From((string charName) => OnCharacterDied(charName)));

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

		// ── "New Run" button ──────────────────────────────────────────────────
		var button = new Button();
		button.Text = "New Run";
		button.CustomMinimumSize = new Vector2(180f, 52f);
		button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		button.AddThemeFontSizeOverride("font_size", 18);
		button.AddThemeColorOverride("font_color", new Color(0.90f, 0.87f, 0.83f));

		// Normal state — dark panel with a muted red border
		var normalStyle = new StyleBoxFlat();
		normalStyle.BgColor = new Color(0.16f, 0.11f, 0.11f);
		normalStyle.SetCornerRadiusAll(6);
		normalStyle.SetBorderWidthAll(2);
		normalStyle.BorderColor = new Color(0.50f, 0.20f, 0.20f);
		normalStyle.ContentMarginLeft = 16f;
		normalStyle.ContentMarginRight = 16f;
		normalStyle.ContentMarginTop = 10f;
		normalStyle.ContentMarginBottom = 10f;

		// Hover state — lighter with a brighter red border
		var hoverStyle = new StyleBoxFlat();
		hoverStyle.BgColor = new Color(0.26f, 0.16f, 0.16f);
		hoverStyle.SetCornerRadiusAll(6);
		hoverStyle.SetBorderWidthAll(2);
		hoverStyle.BorderColor = new Color(0.80f, 0.30f, 0.30f);
		hoverStyle.ContentMarginLeft = 16f;
		hoverStyle.ContentMarginRight = 16f;
		hoverStyle.ContentMarginTop = 10f;
		hoverStyle.ContentMarginBottom = 10f;

		button.AddThemeStyleboxOverride("normal", normalStyle);
		button.AddThemeStyleboxOverride("hover", hoverStyle);
		button.AddThemeStyleboxOverride("pressed", normalStyle);
		button.AddThemeStyleboxOverride("focus", normalStyle);

		button.Pressed += OnNewRunPressed;
		vbox.AddChild(button);
	}

	// ── public API ────────────────────────────────────────────────────────────

	/// <summary>
	/// Makes the overlay visible and pauses all game logic.
	/// Safe to call multiple times (subsequent calls are no-ops).
	/// </summary>
	public void ShowDeathScreen()
	{
		if (Visible) return;
		Visible = true;
		GetTree().Paused = true;
	}

	// ── private ───────────────────────────────────────────────────────────────

	void OnNewRunPressed()
	{
		// Unpause first so the scene tree is in a clean state before reload.
		GetTree().Paused = false;

		// Clear static signal state so the fresh scene starts with clean tables.
		GlobalAutoLoad.Reset();

		GetTree().ReloadCurrentScene();
	}
}
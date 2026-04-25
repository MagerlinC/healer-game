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
/// Extends <see cref="LoadoutController"/> so it inherits the full spell/talent
/// overlay system.  Overworld-specific additions:
///   • Library background + walkable player
///   • Run History scroll interactible + overlay
///   • Map interactible — opens MapScreen to start or continue the run
///
/// The old Door interactible is replaced by the Map (map.png asset).
/// </summary>
public partial class OverworldController : LoadoutController
{
	// ── overworld-only panels ─────────────────────────────────────────────────
	CanvasLayer? _historyPanel;
	VBoxContainer? _historyContent;

	// ── SetupScene ────────────────────────────────────────────────────────────

	protected override void SetupScene()
	{
		var bg = new Sprite2D();
		bg.Texture = GD.Load<Texture2D>(AssetConstants.OverworldBackgroundPath);
		bg.Centered = true;
		bg.Position = new Vector2(1920f / 2f, 1080f / 2f);
		bg.Scale = new Vector2(0.5f, 0.5f);
		AddChild(bg);

		var bgHalfW = bg.Texture.GetWidth() * bg.Scale.X / 2f;
		var bgLeft = bg.Position.X - bgHalfW;
		var bgRight = bg.Position.X + bgHalfW;

		// ── Interactibles ─────────────────────────────────────────────────────
		var spellTome = MakeInteractible(AssetConstants.SpellTomeInteractiblePath,
			new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f);
		var talentBoard = MakeInteractible(AssetConstants.TalentBoardInteractiblePath,
			new Vector2(796f, FloorHeight), new Vector2(0.090f, 0.090f), 50f);
		var historyScroll = MakeInteractible(AssetConstants.RunScrollInteractiblePath,
			new Vector2(696f, FloorHeight), new Vector2(0.055f, 0.055f), 28f);
		var mapItem = MakeInteractible(AssetConstants.MapInteractiblePath,
			new Vector2(525f, FloorHeight), new Vector2(0.100f, 0.100f), 28f);
		mapItem.Scale = new Vector2(1.2f, 1.2f);

		AddChild(spellTome);
		AddChild(talentBoard);
		AddChild(historyScroll);
		AddChild(mapItem);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);
		_interactibles.Add(historyScroll);
		_interactibles.Add(mapItem);

		// ── Run History panel (overworld-only) ────────────────────────────────
		_historyPanel = BuildOverlayPanel("Run History", BuildRunHistoryPane());
		_panels.Add(_historyPanel);
		AddChild(_historyPanel);

		// ── Player ────────────────────────────────────────────────────────────
		_player = new OverworldPlayer();
		_player.Position = new Vector2(896f, FloorHeight - 15f);
		_player.Scale = new Vector2(1.5f, 1.5f);
		_player.XMin = bgLeft;
		_player.XMax = bgRight;
		AddChild(_player);

		// ── HUD ───────────────────────────────────────────────────────────────
		var hud = new CanvasLayer { Layer = 5 };
		AddChild(hud);
		hud.AddChild(BuildHintLabel());
		hud.AddChild(BuildBackToMenuButton());
		_characterProgressLabel = BuildCharacterProgressLabel();
		hud.AddChild(_characterProgressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		spellTome.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_spellPanel!);
		};
		spellTome.MouseEntered += () => _hintLabel!.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited += () => _hintLabel!.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_talentPanel!);
		};
		talentBoard.MouseEntered += () => _hintLabel!.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited += () => _hintLabel!.Text = DefaultHint;

		historyScroll.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenHistoryPanel();
		};
		historyScroll.MouseEntered += () => _hintLabel!.Text = "Run History  •  Click to open";
		historyScroll.MouseExited += () => _hintLabel!.Text = DefaultHint;

		mapItem.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OnOpenMap();
		};
		mapItem.MouseEntered += () => _hintLabel!.Text = "World Map  •  Plan your journey";
		mapItem.MouseExited += () => _hintLabel!.Text = DefaultHint;
	}

	void OpenHistoryPanel()
	{
		RebuildHistoryContent();
		OpenPanel(_historyPanel!);
	}

	void OnOpenMap()
	{
		GetTree().ChangeSceneToFile("res://levels/MapScreen.tscn");
	}

	// ── Main Menu override (no run in progress from Overworld) ────────────────
	protected override void OnMainMenuPressed()
	{
		// From Overworld there is no active run, so just reset and navigate.
		GlobalAutoLoad.Reset();
		RunState.Instance.Reset();
		GetTree().ChangeSceneToFile("res://levels/MainMenu.tscn");
	}

	// ══════════════════════════════════════════════════════════════════════════
	// RUN HISTORY PANE  (overworld-only)
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

		RebuildHistoryContent();
		return margin;
	}

	void RebuildHistoryContent()
	{
		if (_historyContent == null) return;
		foreach (var child in _historyContent.GetChildren()) child.QueueFree();

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

		var header = new HBoxContainer();
		header.AddThemeConstantOverride("separation", 12);
		vbox.AddChild(header);

		var runLabel = new Label();
		runLabel.Text = $"Run #{runNumber}  •  {run.CompletedAt:MMM d, yyyy  h:mm tt}";
		runLabel.AddThemeFontSizeOverride("font_size", 14);
		runLabel.AddThemeColorOverride("font_color", new Color(0.82f, 0.78f, 0.72f));
		runLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		header.AddChild(runLabel);

		var m = (int)run.Duration.TotalMinutes;
		var s = run.Duration.Seconds;
		var durationLabel = new Label();
		durationLabel.Text = $"{m}:{s:D2}";
		durationLabel.AddThemeFontSizeOverride("font_size", 13);
		durationLabel.AddThemeColorOverride("font_color", HintColor);
		header.AddChild(durationLabel);

		var outcome = new Label();
		outcome.Text = run.IsVictory ? "VICTORY" : "DEFEAT";
		outcome.AddThemeFontSizeOverride("font_size", 14);
		outcome.AddThemeColorOverride("font_color",
			run.IsVictory ? new Color(0.40f, 0.85f, 0.35f) : new Color(0.85f, 0.28f, 0.22f));
		header.AddChild(outcome);

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
}
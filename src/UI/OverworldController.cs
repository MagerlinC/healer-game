#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Root script for the Overworld scene.
///
/// Extends <see cref="LoadoutController"/> so it inherits the full spell/talent
/// overlay system.  Overworld-specific additions:
///   • Library background + walkable player
///   • Run History scroll interactible + overlay (see <c>OverworldController.RunHistory.cs</c>)
///   • Map interactible — opens MapScreen to start or continue the run
///   • First-time tutorial popup via <see cref="TutorialPopup"/>
/// </summary>
public partial class OverworldController : LoadoutController
{
	RuneTablePanel? _runeTablePanel;
	AudioStreamPlayer _sfxPlayer = null!;

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		_sfxPlayer = new AudioStreamPlayer();
		_sfxPlayer.VolumeDb = -6f;
		AddChild(_sfxPlayer);
		AddChild(new TutorialPopup());
	}

	// ── SetupScene ────────────────────────────────────────────────────────────

	protected override void SetupScene()
	{
		// Scale the background to cover the full visible world area so it fills
		// any aspect ratio (e.g. ultrawide) without black bars or distortion.
		var camera = GetNode<Camera2D>("Camera2D");
		var viewSize = GetViewport().GetVisibleRect().Size;
		var worldW = viewSize.X / camera.Zoom.X;
		var worldH = viewSize.Y / camera.Zoom.Y;

		var bg = new Sprite2D();
		bg.Texture = GD.Load<Texture2D>(AssetConstants.OverworldBackgroundPath);
		bg.Centered = true;
		bg.Position = camera.Position;
		var bgScale = Mathf.Max(worldW / bg.Texture.GetWidth(), worldH / bg.Texture.GetHeight());
		bg.Scale = new Vector2(bgScale, bgScale);
		AddChild(bg);

		var bgLeft = camera.Position.X - worldW / 2f;
		var bgRight = camera.Position.X + worldW / 2f;

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

		// Rune table — placed to the right of the spell tome.
		var runeTable = MakeInteractible(AssetConstants.RuneTableInteractiblePath,
			new Vector2(1110f, FloorHeight), new Vector2(0.085f, 0.085f), 36f);

		AddChild(spellTome);
		AddChild(talentBoard);
		AddChild(historyScroll);
		AddChild(mapItem);
		AddChild(runeTable);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);
		_interactibles.Add(historyScroll);
		_interactibles.Add(mapItem);
		_interactibles.Add(runeTable);

		// ── Run History panel (see OverworldController.RunHistory.cs) ─────────
		(_historyPanel, _) = BuildOverlayPanel("Run History", BuildRunHistoryPane());
		_panels.Add(_historyPanel);
		AddChild(_historyPanel);

		// ── Rune Table panel ──────────────────────────────────────────────────
		_runeTablePanel = new RuneTablePanel();
		_panels.Add(_runeTablePanel);
		AddChild(_runeTablePanel);

		// ── Encounter detail modal (layer 15 — above the history panel) ───────
		_detailModalLayer = BuildDetailModal();
		AddChild(_detailModalLayer);

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
			if (IsLeftClick(ev))
			{
				PlayerProgressStore.MarkSpellbookOpened();
				OpenPanel(_spellPanel!);
				_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.SpellbookSfxPath);
				_sfxPlayer.Play();
			}
		};
		spellTome.MouseEntered += () => _hintLabel!.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited += () => _hintLabel!.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev))
			{
				OpenPanel(_talentPanel!);
				_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.TalentsSfxPath);
				_sfxPlayer.Play();
			}
		};
		talentBoard.MouseEntered += () => _hintLabel!.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited += () => _hintLabel!.Text = DefaultHint;

		historyScroll.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev))
			{
				OpenHistoryPanel();
				_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.SpellbookSfxPath);
				_sfxPlayer.Play();
			}
		};
		historyScroll.MouseEntered += () => _hintLabel!.Text = "Run History  •  Click to open";
		historyScroll.MouseExited += () => _hintLabel!.Text = DefaultHint;

		mapItem.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OnOpenMap();
		};
		mapItem.MouseEntered += () => _hintLabel!.Text = "World Map  •  Plan your journey";
		mapItem.MouseExited += () => _hintLabel!.Text = DefaultHint;

		runeTable.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev))
			{
				_runeTablePanel!.Open();
				_sfxPlayer.Stream = GD.Load<AudioStream>(AssetConstants.RuneSfxPath);
				_sfxPlayer.Play();
			}
		};
		runeTable.MouseEntered += () => _hintLabel!.Text = "Rune Table  •  Configure difficulty runes";
		runeTable.MouseExited += () => _hintLabel!.Text = DefaultHint;

		// ── Dev boss popup (Ctrl+Alt+O) — only available in debug builds ────────
		if (OS.IsDebugBuild())
			AddChild(new DevBossPopup());
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
}
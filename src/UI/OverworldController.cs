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

	// ── lifecycle ─────────────────────────────────────────────────────────────

	public override void _Ready()
	{
		base._Ready();
		AddChild(new TutorialPopup());
	}

	// ── SetupScene ────────────────────────────────────────────────────────────

	protected override void SetupScene()
	{
		var (bgLeft, bgRight) = SetupBackground(AssetConstants.OverworldBackgroundPath);

		// ── Interactibles ─────────────────────────────────────────────────────
		var spellTome = AddInteractible(new InteractibleObject(
			AssetConstants.SpellTomeInteractiblePath,
			new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f,
			AssetConstants.SpellbookSfxPath));

		var talentBoard = AddInteractible(new InteractibleObject(
			AssetConstants.TalentBoardInteractiblePath,
			new Vector2(796f, FloorHeight), new Vector2(0.090f, 0.090f), 50f,
			AssetConstants.TalentsSfxPath));

		var historyScroll = AddInteractible(new InteractibleObject(
			AssetConstants.RunScrollInteractiblePath,
			new Vector2(696f, FloorHeight), new Vector2(0.055f, 0.055f), 28f,
			AssetConstants.SpellbookSfxPath));

		var mapItem = AddInteractible(new InteractibleObject(
			AssetConstants.MapInteractiblePath,
			new Vector2(525f, FloorHeight), new Vector2(0.100f, 0.100f), 28f));
		mapItem.Scale = new Vector2(1.2f, 1.2f);

		var runeTable = AddInteractible(new InteractibleObject(
			AssetConstants.RuneTableInteractiblePath,
			new Vector2(1110f, FloorHeight), new Vector2(0.085f, 0.085f), 36f,
			AssetConstants.RuneSfxPath));

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
		SetupPlayer(896f, bgLeft, bgRight);

		// ── HUD ───────────────────────────────────────────────────────────────
		SetupHud();

		// ── Wire interactible clicks ──────────────────────────────────────────
		spellTome.Interacted += () =>
		{
			PlayerProgressStore.MarkSpellbookOpened();
			OpenPanel(_spellPanel!);
		};
		WireHints(spellTome, "Spellbook  •  Click to open");

		talentBoard.Interacted += () => OpenPanel(_talentPanel!);
		WireHints(talentBoard, "Talent Board  •  Click to open");

		historyScroll.Interacted += OpenHistoryPanel;
		WireHints(historyScroll, "Run History  •  Click to open");

		mapItem.Interacted += OnOpenMap;
		WireHints(mapItem, "World Map  •  Plan your journey");

		runeTable.Interacted += () => _runeTablePanel!.Open();
		WireHints(runeTable, "Rune Table  •  Configure difficulty runes");

		// ── Dev boss popup (Ctrl+Alt+O) — only available in debug builds ─────
		if (OS.IsDebugBuild())
			AddChild(new DevBossPopup());
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

#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Root script for the Overworld scene.
///
/// Extends <see cref="LoadoutController"/> so it inherits the full spell
/// overlay system.  Overworld-specific additions:
///   • Library background + walkable player
///   • Run History scroll interactible + overlay (see <c>OverworldController.RunHistory.cs</c>)
///   • Rune Table interactible
///   • First-time tutorial popup via <see cref="TutorialPopup"/>
///
/// The spell tome, talent board, map, and news board are handled by the
/// shared <see cref="LoadoutController.SetupCommonInteractibles"/> method.
///
/// Talents are earned during each run via the victory screen after defeating bosses.
/// School affinity can be set here (before or between runs) or changed at Camp.
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
		var (bgLeft, bgRight) = SetupBackground(AssetConstants.CampBackgroundPath);

		// ── Common interactibles (Spell Tome, Talent Board, Map, News Board) ──
		SetupCommonInteractibles();

		// ── Overworld-specific interactibles ──────────────────────────────────
		var runHistoryScroll = AddInteractible(new InteractibleObject(
			AssetConstants.RunScrollInteractiblePath,
			new Vector2(696f, FloorHeight - 8f), new Vector2(0.075f, 0.075f), 28f,
			AssetConstants.SpellbookSfxPath));

		if (PlayerProgressStore.HasUnlockedRuneEntry)
		{
			var runeTable = AddInteractible(new InteractibleObject(
				AssetConstants.RuneTableInteractiblePath,
				new Vector2(1110f, FloorHeight), new Vector2(0.085f, 0.085f), 36f,
				AssetConstants.RuneSfxPath));

			runeTable.Interacted += () => _runeTablePanel!.Open();
			WireHints(runeTable, "Rune Table  •  Configure difficulty runes");
		}

		// ── Run History panel (see OverworldController.RunHistory.cs) ─────────
		(_historyPanel, _) = BuildOverlayPanel("Run History", BuildRunHistoryPane());
		_panels.Add(_historyPanel);
		AddChild(_historyPanel);

		// ── Rune Table panel ──────────────────────────────────────────────────
		_runeTablePanel = new RuneTablePanel();
		_panels.Add(_runeTablePanel);
		AddChild(_runeTablePanel);

		// ── Player ────────────────────────────────────────────────────────────
		SetupPlayer(896f, bgLeft, bgRight);

		// ── HUD ───────────────────────────────────────────────────────────────
		SetupHud();

		// ── Wire overworld-specific interactibles ─────────────────────────────
		runHistoryScroll.Interacted += OpenHistoryPanel;
		WireHints(runHistoryScroll, "Run History  •  Click to open");

		// ── Dev boss popup (Ctrl+Alt+O) — only available in debug builds ─────
		if (OS.IsDebugBuild())
			AddChild(new DevBossPopup());
	}

	// ── Spell Tome — mark first open on Overworld ─────────────────────────────

	protected override void OnSpellTomeInteracted()
	{
		PlayerProgressStore.MarkSpellbookOpened();
		OpenPanel(_spellPanel!);
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
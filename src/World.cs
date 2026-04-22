using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.UI;

/// <summary>
/// Root script for the World scene.
///
/// Reads <see cref="RunState.CurrentBossIndex"/> to determine which boss and
/// arena background to load for this encounter.  The boss scene is instantiated
/// at runtime so a single World scene serves all three encounters.
///
/// Wires each character's HealthChanged signal to the matching PartyUI slot
/// so the bars stay in sync without any character needing to know about the UI.
///
/// Slot order matches PartyUI.MemberDefs:
///   0 = Templar | 1 = Healer (Player) | 2 = Assassin | 3 = Wizard
/// </summary>
public partial class World : Node2D
{
	public override void _Ready()
	{
		// Clear combat log from any previous encounter so the new run's stats
		// are clean. RunHistoryStore.StartRun() is called from OverworldController
		// or DeathScreen (retry), so we only need to wipe the rolling event store.
		CombatLog.Clear();

		// ── Arena background ──────────────────────────────────────────────────
		// Render on a CanvasLayer behind all game content so it fills the screen
		// regardless of the 2D camera position.
		var bgLayer = new CanvasLayer { Layer = -10 };
		AddChild(bgLayer);
		var bgRect = new TextureRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Texture = GD.Load<Texture2D>(AssetConstants.ArenaBackgroundPaths[RunState.Instance.CurrentBossIndex]);
		bgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		bgLayer.AddChild(bgRect);

		// Tooltip singleton must be added first so it is available to all UI nodes.
		AddChild(new GameTooltip());

		// ── Floating combat text ──────────────────────────────────────────────
		var fctManager = new FloatingCombatTextManager();
		AddChild(fctManager);

		var ui = GetNode<GameUI>("PartyUI");
		var player = GetNode<Player>("Healer");
		var templar = GetNode<Character>("Templar");
		var assassin = GetNode<Character>("Assassin");
		var wizard = GetNode<Character>("Wizard");

		// ── Boss — instantiated dynamically from RunState ─────────────────────
		var bossScene = GD.Load<PackedScene>(GameConstants.BossScenePaths[RunState.Instance.CurrentBossIndex]);
		var boss = bossScene.Instantiate<Character>();
		boss.Position = new Vector2(123f, 120f);
		AddChild(boss); // _Ready fires here; boss registers its signals with GlobalAutoLoad

		// Register all characters with the floating combat text manager
		fctManager.Register(player);
		fctManager.Register(templar);
		fctManager.Register(assassin);
		fctManager.Register(wizard);
		fctManager.Register(boss);

		// Bind characters so hovering a frame resolves to the right Character
		ui.BindCharacter(0, templar);
		ui.BindCharacter(1, player);
		ui.BindCharacter(2, assassin);
		ui.BindCharacter(3, wizard);

		// Give the Player a reference to the UI for hover-target resolution
		player.GameUI = ui;

		// Build the action bar from the player's default loadout
		ui.RebuildActionBar(player.EquippedSpells);

		// Build the generic action bar (Dispel + Deflect — always available)
		ui.BuildGenericActionBar(player);

		// ── Spellbook selector ────────────────────────────────────────────────
		// Opens with [B]. Lets the player choose which spells fill their 6 slots.
		var spellbook = new SpellbookSelector();
		AddChild(spellbook);
		spellbook.Init(player, ui);

		// ── Talent selector ───────────────────────────────────────────────────
		var talentSelector = new TalentSelector();
		AddChild(talentSelector);
		talentSelector.Init(player);

		// ── Death screen ──────────────────────────────────────────────────────
		var deathScreen = new DeathScreen();
		AddChild(deathScreen);

		// ── Victory / arena-cleared screen ────────────────────────────────────
		var victoryScreen = new VictoryScreen();
		AddChild(victoryScreen);
	}
}
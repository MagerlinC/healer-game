using System.Collections.Generic;
using Godot;
using healerfantasy;
using healerfantasy.CombatLog;
using healerfantasy.UI;

/// <summary>
/// Root script for the World scene.
///
/// Reads <see cref="RunState.CurrentDungeon"/> and
/// <see cref="RunState.CurrentBossIndexInDungeon"/> to determine which boss
/// and arena background to load for this encounter.
///
/// A single World scene serves all boss fights across all dungeons — the boss
/// and background are instantiated at runtime.
///
/// Multi-boss scenes (e.g. Astral Twins) are supported: when the loaded scene's
/// root is a plain Node2D the code collects all Character children in the boss
/// group and registers each one individually for FCT and health bars.
///
/// Slot order matches PartyUI.MemberDefs:
///   0 = Templar | 1 = Healer (Player) | 2 = Assassin | 3 = Wizard
/// </summary>
public partial class World : Node2D
{
	public override void _Ready()
	{
		CombatLog.Clear();

		var dungeon   = RunState.Instance.CurrentDungeon;
		var bossIndex = RunState.Instance.CurrentBossIndexInDungeon;

		// ── Arena background ──────────────────────────────────────────────────
		var bgLayer = new CanvasLayer { Layer = -10 };
		AddChild(bgLayer);
		var bgRect = new TextureRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Texture     = GD.Load<Texture2D>(dungeon.ArenaBackgroundPaths[bossIndex]);
		bgRect.StretchMode = TextureRect.StretchModeEnum.Scale;
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		bgLayer.AddChild(bgRect);

		// Tooltip singleton must be added first so it is available to all UI nodes.
		AddChild(new GameTooltip());

		// ── Deflect overlay ───────────────────────────────────────────────────
		AddChild(new DeflectOverlay());

		// ── Floating combat text ──────────────────────────────────────────────
		var fctManager = new FloatingCombatTextManager();
		AddChild(fctManager);

		var ui      = GetNode<GameUI>("PartyUI");
		var player  = GetNode<Player>("Healer");
		var templar  = GetNode<Character>("Templar");
		var assassin = GetNode<Character>("Assassin");
		var wizard   = GetNode<Character>("Wizard");

		// ── Boss — instantiated from current dungeon definition ───────────────
		var bossScene = GD.Load<PackedScene>(dungeon.BossScenePaths[bossIndex]);
		var bossRoot  = bossScene.Instantiate();

		// Position the boss (or its container) in the arena.
		if (bossRoot is Node2D bossNode2D)
			bossNode2D.Position = new Vector2(123f, 120f);

		AddChild(bossRoot);

		// Collect all Character nodes that are tagged as bosses.
		// Single-boss scenes: the root IS the Character.
		// Multi-boss scenes (e.g. AstralTwins): root is a plain Node2D with
		// Character children, each of which is already in the boss group via
		// their own _Ready().
		var bossCharacters = new List<Character>();
		if (bossRoot is Character singleBoss)
		{
			bossCharacters.Add(singleBoss);
		}
		else
		{
			foreach (var child in bossRoot.GetChildren())
				if (child is Character c)
					bossCharacters.Add(c);
		}

		foreach (var b in bossCharacters)
			fctManager.Register(b);

		// Register boss character refs for hover-targeting and health bar management.
		ui.SetBossCharacters(
			bossCharacters[0],
			bossCharacters.Count > 1 ? bossCharacters[1] : null);

		// If there is more than one boss (e.g. Astral Twins), show a secondary
		// health bar for the second boss beneath the primary one.
		if (bossCharacters.Count > 1)
			ui.ShowSecondaryBossBar(bossCharacters[1]);

		fctManager.Register(player);
		fctManager.Register(templar);
		fctManager.Register(assassin);
		fctManager.Register(wizard);

		ui.BindCharacter(0, templar);
		ui.BindCharacter(1, player);
		ui.BindCharacter(2, assassin);
		ui.BindCharacter(3, wizard);

		player.GameUI = ui;

		ui.RebuildActionBar(player.EquippedSpells);
		ui.BuildGenericActionBar(player);

		// ── Spellbook selector ────────────────────────────────────────────────
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

		// ── Victory / dungeon-cleared screen ──────────────────────────────────
		var victoryScreen = new VictoryScreen();
		AddChild(victoryScreen);

		// ── Pause menu (Escape key) ────────────────────────────────────────────
		var pauseMenu = new healerfantasy.UI.PauseMenu();
		AddChild(pauseMenu);
	}
}

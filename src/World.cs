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
		var boss      = bossScene.Instantiate<Character>();
		boss.Position = new Vector2(123f, 120f);
		AddChild(boss);

		fctManager.Register(player);
		fctManager.Register(templar);
		fctManager.Register(assassin);
		fctManager.Register(wizard);
		fctManager.Register(boss);

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
	}
}

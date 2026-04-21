using Godot;
using healerfantasy;
using healerfantasy.UI;

/// <summary>
/// Root script for the World scene.
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

		// Register all characters with the floating combat text manager
		fctManager.Register(player);
		fctManager.Register(templar);
		fctManager.Register(assassin);
		fctManager.Register(wizard);
		fctManager.Register(GetNode<Character>("CrystalKnight"));

		// Bind characters so hovering a frame resolves to the right Character
		ui.BindCharacter(0, templar);
		ui.BindCharacter(1, player);
		ui.BindCharacter(2, assassin);
		ui.BindCharacter(3, wizard);

		// Give the Player a reference to the UI for hover-target resolution
		player.GameUI = ui;

		// Build the action bar from the player's default loadout
		ui.RebuildActionBar(player.EquippedSpells);

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

		// ── Victory screen ────────────────────────────────────────────────────
		var victoryScreen = new VictoryScreen();
		AddChild(victoryScreen);
	}
}

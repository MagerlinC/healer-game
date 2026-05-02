#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.UI;

/// <summary>
/// Root script for the Camp scene — a mid-run rest stop where the player can
/// adjust their spell and talent loadout before heading to the next dungeon.
///
/// Extends <see cref="LoadoutController"/> to inherit the full spell/talent
/// overlay system.  Camp-specific additions:
///   • Camp background
///   • Four interactibles: Map, Spell Tome, Talent Board, Armory
///   • Map click → navigate to MapScreen to select the next dungeon
///   • Armory click → open item equip/unequip panel (item management)
///
/// Note: <see cref="RunState.CompleteCamp"/> is called by MapScreenController
/// when the player actually clicks a dungeon node, not here — so clicking Map
/// opens the map without committing the camp-departure state yet.
/// </summary>
public partial class CampController : LoadoutController
{
	CanvasLayer? _armoryPanel;
	EquipmentPane? _equipmentPane;

	protected override void SetupScene()
	{
		var (bgLeft, bgRight) = SetupBackground(AssetConstants.OverworldBackgroundPath);

		// ── Armory overlay panel ──────────────────────────────────────────────
		// Built before the interactibles so the panel reference is ready to wire.
		_equipmentPane = new EquipmentPane();
		(_armoryPanel, _) = BuildOverlayPanel("Armory", _equipmentPane);
		_panels.Add(_armoryPanel);
		AddChild(_armoryPanel);

		// ── Interactibles ─────────────────────────────────────────────────────
		var spellTome = AddInteractible(new InteractibleObject(
			AssetConstants.SpellTomeInteractiblePath,
			new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f,
			AssetConstants.SpellbookSfxPath));

		var talentBoard = AddInteractible(new InteractibleObject(
			AssetConstants.TalentBoardInteractiblePath,
			new Vector2(796f, FloorHeight), new Vector2(0.090f, 0.090f), 50f,
			AssetConstants.TalentsSfxPath));

		var armory = AddInteractible(new InteractibleObject(
			AssetConstants.ArmoryInteractiblePath,
			new Vector2(696f, FloorHeight - 12f), new Vector2(0.125f, 0.125f), 36f));

		var mapItem = AddInteractible(new InteractibleObject(
			AssetConstants.MapInteractiblePath,
			new Vector2(525f, FloorHeight), new Vector2(0.125f, 0.125f), 28f));

		// ── Player ────────────────────────────────────────────────────────────
		SetupPlayer(660f, bgLeft, bgRight);

		// ── HUD ───────────────────────────────────────────────────────────────
		var hud = SetupHud();

		// Dungeon progress label (e.g. "Rest · 1 of 3 dungeons cleared")
		var progressLabel = new Label();
		progressLabel.Text = BuildProgressText();
		progressLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		progressLabel.OffsetLeft = 20f;
		progressLabel.OffsetTop = 80f;
		progressLabel.AddThemeFontSizeOverride("font_size", 15);
		progressLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 0.45f));
		progressLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hud.AddChild(progressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		spellTome.Interacted += () => OpenPanel(_spellPanel!);
		WireHints(spellTome, "Spellbook  •  Click to open");

		talentBoard.Interacted += () => OpenPanel(_talentPanel!);
		WireHints(talentBoard, "Talent Board  •  Click to open");

		armory.Interacted += OpenArmory;
		WireHints(armory, "Armory  •  Manage your equipped items");

		mapItem.Interacted += OnOpenMap;
		WireHints(mapItem, "World Map  •  Continue your journey");
	}

	// ── armory panel ──────────────────────────────────────────────────────────

	void OpenArmory()
	{
		// Refresh EquipmentPane from ItemStore each time the panel is opened.
		_equipmentPane!.Refresh();
		OpenPanel(_armoryPanel!);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static string BuildProgressText()
	{
		var d = RunState.Instance.CompletedDungeons;
		var total = RunState.Instance.RunDungeons.Count;
		return $"Rest  ·  {d} of {total} dungeons cleared";
	}
}

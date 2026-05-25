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
///   • Armory interactible + overlay (item equip/unequip management)
///   • Merchant interactible + overlay (buy/sell items for gold)
///   • Dungeon progress label in the HUD
///   • Camp merchant first-visit tutorial
///
/// The spell tome, talent board, map, and news board are handled by the
/// shared <see cref="LoadoutController.SetupCommonInteractibles"/> method.
///
/// Note: <see cref="RunState.CompleteCamp"/> is called by MapScreenController
/// when the player actually clicks a dungeon node, not here — so clicking Map
/// opens the map without committing the camp-departure state yet.
/// </summary>
public partial class CampController : LoadoutController
{
	CanvasLayer? _armoryPanel;
	EquipmentPane? _equipmentPane;
	CanvasLayer? _merchantPanel;
	MerchantPane? _merchantPane;
	InteractibleObject? _merchantInteractible;

	protected override bool PersistSpellLoadout => false;

	// ── Virtual config overrides (Camp layout differs slightly from Overworld) ─

	protected override float MapTextureScale => 0.125f;
	protected override string MapHintText => "World Map  •  Continue your journey";

	// ── SetupScene ────────────────────────────────────────────────────────────

	protected override void SetupScene()
	{
		var (bgLeft, bgRight) = SetupBackground(AssetConstants.CampBackgroundPath);

		// ── Armory overlay panel ──────────────────────────────────────────────
		// Built before the interactibles so the panel reference is ready to wire.
		_equipmentPane = new EquipmentPane();
		(_armoryPanel, _) = BuildOverlayPanel("Armory", _equipmentPane);
		_panels.Add(_armoryPanel);
		AddChild(_armoryPanel);

		// ── Merchant overlay panel ────────────────────────────────────────────
		// Generate this camp's item stock before the panel is built so that the
		// first Refresh() call (triggered when the player opens the shop) has
		// items ready to display.
		healerfantasy.Merchant.MerchantStore.GenerateStock();
		_merchantPane = new MerchantPane();
		(_merchantPanel, _) = BuildOverlayPanel("The Merchant", _merchantPane);
		_panels.Add(_merchantPanel);
		AddChild(_merchantPanel);

		// ── Common interactibles (Spell Tome, Talent Board, Map, News Board) ──
		SetupCommonInteractibles();

		// ── Camp-specific interactibles ───────────────────────────────────────
		var armory = AddInteractible(new InteractibleObject(
			AssetConstants.ArmoryInteractiblePath,
			new Vector2(696f, FloorHeight - 12f), new Vector2(0.125f, 0.125f), 36f));

		_merchantInteractible = AddInteractible(new InteractibleObject(
			AssetConstants.MerchantInteractiblePath,
			new Vector2(1400f, FloorHeight - 30f), new Vector2(0.20f, 0.20f), 36f));
		var merchant = _merchantInteractible;

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

		// ── Wire camp-specific interactibles ──────────────────────────────────
		armory.Interacted += OpenArmory;
		WireHints(armory, "Armory  •  Manage your equipped items");

		merchant.Interacted += OpenMerchant;
		WireHints(merchant, "Merchant  •  Buy and sell items for gold");

		// Show the camp merchant tutorial the first time the player visits camp.
		// Deferred so the scene is fully laid out before we query node positions.
		if (!PlayerProgressStore.HasSeenCampMerchantTutorial)
			Callable.From(ShowCampMerchantTutorial).CallDeferred();
	}

	// ── armory panel ──────────────────────────────────────────────────────────

	void OpenArmory()
	{
		// Refresh EquipmentPane from ItemStore each time the panel is opened.
		_equipmentPane!.Refresh();
		OpenPanel(_armoryPanel!);
	}

	// ── merchant panel ────────────────────────────────────────────────────────

	void OpenMerchant()
	{
		_merchantPane!.Refresh();
		OpenPanel(_merchantPanel!);
	}

	// ── camp merchant tutorial ────────────────────────────────────────────────

	void ShowCampMerchantTutorial()
	{
		if (PlayerProgressStore.HasSeenCampMerchantTutorial) return;

		// Build a spotlight rect from the merchant's world position.
		// The merchant is a Node2D sprite, so we approximate a bounding box
		// centred on its global position.
		var spotlight = new Rect2();
		if (_merchantInteractible != null)
		{
			var centre = _merchantInteractible.GlobalPosition;
			var size = new Vector2(130f, 170f);
			spotlight = new Rect2(centre - size * 0.5f, size);
		}

		TutorialHighlightOverlay.Show(
			GetTree(),
			"Welcome to Camp!",
			"In the camp, you can review the items and talents you have found so far, and prepare for your next battle by changing your spells at the spell table.\n\n" +
			"The Camp Merchant sells useful items between dungeons.\n" +
			"Click an item to buy it with gold. You can also sell items you no longer need.\n\n" +
			"Stock is refreshed each time you rest at camp.",
			spotlight,
			null,
			PlayerProgressStore.MarkCampMerchantTutorialSeen
		);
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static string BuildProgressText()
	{
		var d = RunState.Instance.CompletedDungeons;
		var total = RunState.Instance.RunDungeons.Count;
		return $"Rest  ·  {d} of {total} dungeons cleared";
	}
}
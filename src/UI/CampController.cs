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
	InteractibleObject? _talentBoard;
	NewsBoardPane? _newsBoardPane;
	CanvasLayer? _newsBoardPanel;
	CanvasLayer? _merchantPanel;
	MerchantPane? _merchantPane;
	InteractibleObject? _merchantInteractible;

	protected override bool PersistSpellLoadout => false;

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

		// ── Interactibles ─────────────────────────────────────────────────────
		var spellTome = AddInteractible(new InteractibleObject(
			AssetConstants.SpellTomeInteractiblePath,
			new Vector2(996f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f,
			AssetConstants.SpellbookSfxPath));

		_talentBoard = AddInteractible(new InteractibleObject(
			AssetConstants.GetTalentBoardPathForAffinity(RunState.Instance.SchoolAffinity),
			new Vector2(796f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 50f,
			AssetConstants.TalentsSfxPath));

		var armory = AddInteractible(new InteractibleObject(
			AssetConstants.ArmoryInteractiblePath,
			new Vector2(696f, FloorHeight - 12f), new Vector2(0.125f, 0.125f), 36f));

		_merchantInteractible = AddInteractible(new InteractibleObject(
			AssetConstants.MerchantInteractiblePath,
			new Vector2(1400f, FloorHeight - 30f), new Vector2(0.20f, 0.20f), 36f));
		var merchant = _merchantInteractible;

		var mapItem = AddInteractible(new InteractibleObject(
			AssetConstants.MapInteractiblePath,
			new Vector2(525f, FloorHeight - 8f), new Vector2(0.125f, 0.125f), 28f));
		mapItem.Scale = new Vector2(1.5f, 1.5f);

		const float NewsBoardX = 585f;
		var newsBoard = AddInteractible(new InteractibleObject(
			AssetConstants.NewsBoardInteractiblePath,
			new Vector2(NewsBoardX, FloorHeight - 18f), new Vector2(0.090f, 0.090f), 32f,
			AssetConstants.SpellbookSfxPath));

		var exclamation = new Sprite2D
		{
			Texture = GD.Load<Texture2D>(AssetConstants.ExclamationInteractiblePath),
			Scale = new Vector2(0.045f, 0.045f),
			Position = new Vector2(NewsBoardX + 26f, FloorHeight - 52f),
			Visible = PlayerProgressStore.HasUnreadBoardEntries
		};
		AddChild(exclamation);

		// ── News Board panel ──────────────────────────────────────────────────
		_newsBoardPane = new NewsBoardPane { ExclamationSprite = exclamation };
		(_newsBoardPanel, _) = BuildOverlayPanel("News Board", _newsBoardPane);
		_panels.Add(_newsBoardPanel);
		AddChild(_newsBoardPanel);

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

		_talentBoard.Interacted += () => OpenPanel(_talentPanel!);
		WireHints(_talentBoard, "Talent Board  •  Click to open");

		armory.Interacted += OpenArmory;
		WireHints(armory, "Armory  •  Manage your equipped items");

		merchant.Interacted += OpenMerchant;
		WireHints(merchant, "Merchant  •  Buy and sell items for gold");

		mapItem.Interacted += OnOpenMap;
		WireHints(mapItem, "World Map  •  Continue your journey");

		newsBoard.Interacted += () =>
		{
			_newsBoardPane!.ResetToTopicList();
			OpenPanel(_newsBoardPanel!);
		};
		WireHints(newsBoard, "News Board  •  Discoveries & tips");

		// Show the camp merchant tutorial the first time the player visits camp.
		// Deferred so the scene is fully laid out before we query node positions.
		if (!PlayerProgressStore.HasSeenCampMerchantTutorial)
			Callable.From(ShowCampMerchantTutorial).CallDeferred();
	}

	// ── Affinity change ───────────────────────────────────────────────────────

	protected override void OnAffinityChanged()
	{
		_talentBoard?.SetTexture(
			AssetConstants.GetTalentBoardPathForAffinity(RunState.Instance.SchoolAffinity));
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
			"The Merchant",
			"The Merchant sells useful items between dungeons.\n\n" +
			"Click an item to buy it with gold. You can also sell items you no longer need to free up inventory space.\n\n" +
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
#nullable enable
using System;
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
///   • Camp background (camp.png)
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

	protected override void SetupScene()
	{
		// ── Camera (matches Overworld zoom so background fills the screen) ────
		var camera = new Camera2D();
		camera.Zoom = new Vector2(2f, 2f);
		//AddChild(camera);

		// ── Camp background ───────────────────────────────────────────────────
		var bg = new Sprite2D();
		bg.Texture = GD.Load<Texture2D>(AssetConstants.CampBackgroundPath);
		bg.Centered = true;
		bg.Position = new Vector2(1920f / 2f, 1080f / 2f);
		bg.Scale = new Vector2(0.5f, 0.5f);
		AddChild(bg);

		var bgHalfW = bg.Texture.GetWidth() * bg.Scale.X / 2f;
		var bgLeft = bg.Position.X - bgHalfW;
		var bgRight = bg.Position.X + bgHalfW;

		// ── Armory overlay panel ──────────────────────────────────────────────
		// Built before the interactibles so the panel reference is ready to wire.
		_armoryPanel = BuildOverlayPanel("Armory", BuildArmoryPane());
		_panels.Add(_armoryPanel);
		AddChild(_armoryPanel);

		// ── Interactibles ─────────────────────────────────────────────────────
		// Map is on the left so it's prominent — the player came here to rest,
		// spell/talent editing is in the middle/right, Armory on far right.
		var mapItem = MakeInteractible(AssetConstants.MapInteractiblePath,
			new Vector2(440f, FloorHeight - 20f), new Vector2(0.100f, 0.100f), 28f);
		var spellTome = MakeInteractible(AssetConstants.SpellTomeInteractiblePath,
			new Vector2(680f, FloorHeight - 12f), new Vector2(0.080f, 0.080f), 28f);
		var talentBoard = MakeInteractible(AssetConstants.TalentBoardInteractiblePath,
			new Vector2(890f, FloorHeight), new Vector2(0.090f, 0.090f), 50f);
		var armory = MakeInteractible(AssetConstants.ArmoryInteractiblePath,
			new Vector2(1120f, FloorHeight - 10f), new Vector2(0.090f, 0.090f), 36f);

		AddChild(mapItem);
		AddChild(spellTome);
		AddChild(talentBoard);
		AddChild(armory);
		_interactibles.Add(mapItem);
		_interactibles.Add(spellTome);
		_interactibles.Add(talentBoard);
		_interactibles.Add(armory);

		// ── Player ────────────────────────────────────────────────────────────
		_player = new OverworldPlayer();
		_player.Position = new Vector2(660f, FloorHeight - 15f);
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

		// Dungeon progress label (e.g. "Dungeon 1 of 3 complete")
		var progressLabel = new Label();
		progressLabel.Text = BuildProgressText();
		progressLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		progressLabel.OffsetLeft = 20f;
		progressLabel.OffsetTop = 40f;
		progressLabel.AddThemeFontSizeOverride("font_size", 15);
		progressLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.65f, 0.45f));
		progressLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hud.AddChild(progressLabel);

		// ── Wire interactible clicks ──────────────────────────────────────────
		mapItem.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OnOpenMap();
		};
		mapItem.MouseEntered += () => _hintLabel!.Text = "World Map  •  Continue your journey";
		mapItem.MouseExited += () => _hintLabel!.Text = DefaultHint;

		spellTome.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_spellPanel!);
		};
		spellTome.MouseEntered += () => _hintLabel!.Text = "Spellbook  •  Click to open";
		spellTome.MouseExited += () => _hintLabel!.Text = DefaultHint;

		talentBoard.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenPanel(_talentPanel!);
		};
		talentBoard.MouseEntered += () => _hintLabel!.Text = "Talent Board  •  Click to open";
		talentBoard.MouseExited += () => _hintLabel!.Text = DefaultHint;

		armory.InputEvent += (_, ev, _) =>
		{
			if (IsLeftClick(ev)) OpenArmory();
		};
		armory.MouseEntered += () => _hintLabel!.Text = "Armory  •  Manage your equipped items";
		armory.MouseExited += () => _hintLabel!.Text = DefaultHint;
	}

	// ── armory panel ──────────────────────────────────────────────────────────

	void OpenArmory()
	{
		// Rebuild the armory content each time so it reflects the current ItemStore state.
		RebuildArmoryPanel();
		OpenPanel(_armoryPanel!);
	}

	/// <summary>
	/// Destroys and recreates the armory panel content so it always shows the
	/// latest ItemStore state when opened.
	/// </summary>
	void RebuildArmoryPanel()
	{
		// Remove old panel and build a fresh one.
		_panels.Remove(_armoryPanel!);
		_armoryPanel!.QueueFree();
		_armoryPanel = BuildOverlayPanel("Armory", BuildArmoryPane());
		_panels.Add(_armoryPanel);
		AddChild(_armoryPanel);
	}

	/// <summary>
	/// Builds the Armory pane content: an equipment slot section on the left,
	/// and an inventory section on the right.
	/// </summary>
	Control BuildArmoryPane()
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_bottom", 8);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 20);
		margin.AddChild(hbox);

		// ── Left: equipped items ──────────────────────────────────────────────
		var equippedCol = new VBoxContainer();
		equippedCol.CustomMinimumSize = new Vector2(260f, 0f);
		equippedCol.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		equippedCol.AddThemeConstantOverride("separation", 10);
		hbox.AddChild(equippedCol);

		var equippedHeader = new Label();
		equippedHeader.Text = "Equipped";
		equippedHeader.AddThemeFontSizeOverride("font_size", 16);
		equippedHeader.AddThemeColorOverride("font_color", TitleColor);
		equippedCol.AddChild(equippedHeader);

		var equippedSep = new HSeparator();
		equippedSep.AddThemeColorOverride("color", SepColor);
		equippedCol.AddChild(equippedSep);

		// One row per equipment slot
		foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
		{
			var equippedItem = ItemStore.GetEquipped(slot);
			equippedCol.AddChild(BuildEquippedSlotRow(slot, equippedItem));
		}

		var equippedFill = new Control();
		equippedFill.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		equippedCol.AddChild(equippedFill);

		// ── Divider ───────────────────────────────────────────────────────────
		var vsep = new VSeparator();
		vsep.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		vsep.AddThemeColorOverride("color", SepColor);
		hbox.AddChild(vsep);

		// ── Right: inventory (unequipped items) ───────────────────────────────
		var inventoryCol = new VBoxContainer();
		inventoryCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		inventoryCol.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		inventoryCol.AddThemeConstantOverride("separation", 10);
		hbox.AddChild(inventoryCol);

		var inventoryHeader = new Label();
		inventoryHeader.Text = "Found Items";
		inventoryHeader.AddThemeFontSizeOverride("font_size", 16);
		inventoryHeader.AddThemeColorOverride("font_color", TitleColor);
		inventoryCol.AddChild(inventoryHeader);

		var inventorySep = new HSeparator();
		inventorySep.AddThemeColorOverride("color", SepColor);
		inventoryCol.AddChild(inventorySep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
		inventoryCol.AddChild(scroll);

		var inventoryList = new VBoxContainer();
		inventoryList.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		inventoryList.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(inventoryList);

		if (ItemStore.Inventory.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No items in inventory.\nItems drop from defeated bosses.";
			empty.AutowrapMode = TextServer.AutowrapMode.Word;
			empty.AddThemeFontSizeOverride("font_size", 13);
			empty.AddThemeColorOverride("font_color", HintColor);
			inventoryList.AddChild(empty);
		}
		else
		{
			foreach (var item in ItemStore.Inventory)
				inventoryList.AddChild(BuildInventoryItemRow(item));
		}

		var hint = new Label();
		hint.Text = "Click an inventory item to equip it into the matching slot.";
		hint.AutowrapMode = TextServer.AutowrapMode.Word;
		hint.HorizontalAlignment = HorizontalAlignment.Center;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		inventoryCol.AddChild(hint);

		return margin;
	}

	/// <summary>Builds one row showing an equipment slot and what (if anything) is equipped.</summary>
	Control BuildEquippedSlotRow(EquipSlot slot, EquippableItem? equippedItem)
	{
		var rowStyle = new StyleBoxFlat();
		rowStyle.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.90f);
		rowStyle.SetCornerRadiusAll(4);
		rowStyle.SetBorderWidthAll(1);
		rowStyle.BorderColor = equippedItem != null
			? RarityColor(equippedItem.Rarity)
			: new Color(0.28f, 0.22f, 0.16f);
		rowStyle.ContentMarginLeft = rowStyle.ContentMarginRight = 10f;
		rowStyle.ContentMarginTop = rowStyle.ContentMarginBottom = 8f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", rowStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.MouseDefaultCursorShape = equippedItem != null
			? Control.CursorShape.PointingHand
			: Control.CursorShape.Arrow;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 8);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(hbox);

		// Slot label
		var slotLabel = new Label();
		slotLabel.Text = slot.ToString();
		slotLabel.CustomMinimumSize = new Vector2(50f, 0f);
		slotLabel.AddThemeFontSizeOverride("font_size", 12);
		slotLabel.AddThemeColorOverride("font_color", HintColor);
		slotLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(slotLabel);

		if (equippedItem != null)
		{
			if (equippedItem.Icon != null)
			{
				var icon = new TextureRect();
				icon.Texture = equippedItem.Icon;
				icon.CustomMinimumSize = new Vector2(32f, 32f);
				icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
				icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
				icon.MouseFilter = Control.MouseFilterEnum.Ignore;
				hbox.AddChild(icon);
			}

			var nameLabel = new Label();
			nameLabel.Text = equippedItem.Name;
			nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			nameLabel.AddThemeFontSizeOverride("font_size", 13);
			nameLabel.AddThemeColorOverride("font_color", RarityColor(equippedItem.Rarity));
			nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			hbox.AddChild(nameLabel);

			var unequipLabel = new Label();
			unequipLabel.Text = "[Unequip]";
			unequipLabel.AddThemeFontSizeOverride("font_size", 11);
			unequipLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.30f, 0.28f));
			unequipLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			hbox.AddChild(unequipLabel);

			panel.MouseEntered += () => GameTooltip.Show(FormatItemTooltip(equippedItem));
			panel.MouseExited += () => GameTooltip.Hide();
			panel.GuiInput += (ev) =>
			{
				if (IsLeftClick(ev))
				{
					ItemStore.Unequip(slot);
					RebuildArmoryPanel();
					OpenPanel(_armoryPanel!);
					panel.AcceptEvent();
				}
			};
		}
		else
		{
			var emptyLabel = new Label();
			emptyLabel.Text = "— Empty —";
			emptyLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			emptyLabel.AddThemeFontSizeOverride("font_size", 13);
			emptyLabel.AddThemeColorOverride("font_color", HintColor);
			emptyLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			hbox.AddChild(emptyLabel);
		}

		return panel;
	}

	/// <summary>Builds a clickable row for an unequipped inventory item.</summary>
	Control BuildInventoryItemRow(EquippableItem item)
	{
		var rowStyle = new StyleBoxFlat();
		rowStyle.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.90f);
		rowStyle.SetCornerRadiusAll(4);
		rowStyle.SetBorderWidthAll(1);
		rowStyle.BorderColor = new Color(0.28f, 0.22f, 0.16f);
		rowStyle.ContentMarginLeft = rowStyle.ContentMarginRight = 10f;
		rowStyle.ContentMarginTop = rowStyle.ContentMarginBottom = 8f;

		var panel = new PanelContainer();
		panel.AddThemeStyleboxOverride("panel", rowStyle);
		panel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		panel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 8);
		hbox.MouseFilter = Control.MouseFilterEnum.Ignore;
		panel.AddChild(hbox);

		if (item.Icon != null)
		{
			var icon = new TextureRect();
			icon.Texture = item.Icon;
			icon.CustomMinimumSize = new Vector2(32f, 32f);
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.MouseFilter = Control.MouseFilterEnum.Ignore;
			hbox.AddChild(icon);
		}

		var textCol = new VBoxContainer();
		textCol.AddThemeConstantOverride("separation", 2);
		textCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		textCol.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(textCol);

		var rarityLabel = new Label();
		rarityLabel.Text = item.Rarity.ToString().ToUpper();
		rarityLabel.AddThemeFontSizeOverride("font_size", 9);
		rarityLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		rarityLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		textCol.AddChild(rarityLabel);

		var nameLabel = new Label();
		nameLabel.Text = item.Name;
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		nameLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		nameLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		textCol.AddChild(nameLabel);

		var equipHint = new Label();
		equipHint.Text = "Click to equip";
		equipHint.AddThemeFontSizeOverride("font_size", 10);
		equipHint.AddThemeColorOverride("font_color", new Color(0.50f, 0.48f, 0.44f));
		equipHint.MouseFilter = Control.MouseFilterEnum.Ignore;
		hbox.AddChild(equipHint);

		panel.MouseEntered += () =>
		{
			rowStyle.BorderColor = RarityColor(item.Rarity);
			GameTooltip.Show(FormatItemTooltip(item));
		};
		panel.MouseExited += () =>
		{
			rowStyle.BorderColor = new Color(0.28f, 0.22f, 0.16f);
			GameTooltip.Hide();
		};
		panel.GuiInput += (ev) =>
		{
			if (IsLeftClick(ev))
			{
				ItemStore.Equip(item);
				RebuildArmoryPanel();
				OpenPanel(_armoryPanel!);
				panel.AcceptEvent();
			}
		};

		return panel;
	}

	static Color RarityColor(ItemRarity rarity)
	{
		return rarity switch
		{
			ItemRarity.Rare => new Color(0.35f, 0.55f, 1.00f),
			ItemRarity.Epic => new Color(0.70f, 0.30f, 0.90f),
			ItemRarity.Legendary => new Color(1.00f, 0.55f, 0.05f),
			_ => new Color(0.80f, 0.78f, 0.72f)
		};
	}

	static string FormatItemTooltip(EquippableItem item)
	{
		return $"{item.Name}\n{item.Rarity}  •  {item.Slot}\n\n{item.Description}";
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static string BuildProgressText()
	{
		var d = RunState.Instance.CompletedDungeons;
		var total = DungeonDefinition.All.Length;
		return $"Rest  ·  {d} of {total} dungeons cleared";
	}

	void OnOpenMap()
	{
		GetTree().ChangeSceneToFile("res://levels/MapScreen.tscn");
	}
}
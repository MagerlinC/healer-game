#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.Merchant;

namespace healerfantasy.UI;

/// <summary>
/// The Merchant's shop panel, opened by clicking the Merchant interactible in Camp.
///
/// Layout (left → right):
///   ┌─ Shop ──────────────────────────┐  │  ┌─ Sell Items ──────────────────────┐
///   │  [icon] Stone of Rebirth   70g  │  │  │  Gold: 45g                        │
///   │         "Revive one fallen…"    │  │  │  ─────────────────────────────     │
///   │         [Buy] / SOLD OUT        │  │  │  [icon] Crystal Staff    sell: 20g │
///   │  ──────────────────────────     │  │  │  [icon] Ruler's Signet   sell: 50g │
///   │  [icon] ArcaneAccelerator  30g  │  │  └───────────────────────────────────┘
///   │         [Buy]                   │
///   └─────────────────────────────────┘
///
/// Buy:  deducts gold, moves item to player inventory (or activates stone).
/// Sell: removes item from player inventory, adds gold.
/// </summary>
public partial class MerchantPane : Control
{
	// ── colour palette ────────────────────────────────────────────────────
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color HintColor = new(0.45f, 0.42f, 0.38f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);
	static readonly Color GoldColor = new(0.95f, 0.84f, 0.30f);
	static readonly Color DimColor = new(0.35f, 0.33f, 0.30f);
	static readonly Color BuyColor = new(0.25f, 0.70f, 0.35f);
	static readonly Color SellColor = new(0.70f, 0.55f, 0.20f);
	static readonly Color DisabledColor = new(0.35f, 0.35f, 0.35f);

	// Live-updated controls
	Label _goldLabel = null!;
	Label _stoneStatusLabel = null!;
	Button _buyStoneButton = null!;
	VBoxContainer _shopStockList = null!;
	VBoxContainer _sellList = null!;

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;

		var outer = new MarginContainer();
		outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		outer.AddThemeConstantOverride("margin_left", 12);
		outer.AddThemeConstantOverride("margin_right", 12);
		outer.AddThemeConstantOverride("margin_top", 6);
		outer.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(outer);

		var root = new VBoxContainer();
		root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddThemeConstantOverride("separation", 10);
		outer.AddChild(root);

		// Flavour quote
		var quote = new Label();
		quote.Text = "\"Ah, a weary healer! Fine wares for the discerning adventurer...\"";
		quote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		quote.HorizontalAlignment = HorizontalAlignment.Center;
		quote.AddThemeFontSizeOverride("font_size", 13);
		quote.AddThemeColorOverride("font_color", HintColor);
		quote.MouseFilter = MouseFilterEnum.Ignore;
		root.AddChild(quote);

		var sep0 = new HSeparator();
		sep0.AddThemeColorOverride("color", SepColor);
		root.AddChild(sep0);

		// Two-column layout
		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 20);
		root.AddChild(hbox);

		hbox.AddChild(BuildShopSection());

		var vsep = new VSeparator();
		vsep.SizeFlagsVertical = SizeFlags.ExpandFill;
		vsep.AddThemeColorOverride("color", SepColor);
		hbox.AddChild(vsep);

		hbox.AddChild(BuildSellSection());
	}

	// ── public API ────────────────────────────────────────────────────────

	/// <summary>
	/// Sync the panel with current RunState / MerchantStore / ItemStore state.
	/// Called each time the panel is opened.
	/// </summary>
	public void Refresh()
	{
		RefreshGoldLabel();
		RefreshStoneStatus();
		RebuildShopStockList();
		RebuildSellList();
	}

	// ── Shop section ──────────────────────────────────────────────────────

	Control BuildShopSection()
	{
		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 8);

		var header = new Label();
		header.Text = "Shop";
		header.AddThemeFontSizeOverride("font_size", 16);
		header.AddThemeColorOverride("font_color", TitleColor);
		vbox.AddChild(header);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		// Stone of Rebirth — permanent fixture at the top of the shop
		vbox.AddChild(BuildStoneOfRebirthRow());

		// Scrollable list for the randomly-stocked items
		var stockSep = new HSeparator();
		stockSep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(stockSep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		vbox.AddChild(scroll);

		_shopStockList = new VBoxContainer();
		_shopStockList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_shopStockList.AddThemeConstantOverride("separation", 12);
		scroll.AddChild(_shopStockList);

		RebuildShopStockList();
		return vbox;
	}

	void RebuildShopStockList()
	{
		if (_shopStockList == null) return;

		foreach (var child in _shopStockList.GetChildren())
			child.QueueFree();

		if (MerchantStore.Stock.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No items in stock today.";
			empty.AddThemeFontSizeOverride("font_size", 13);
			empty.AddThemeColorOverride("font_color", HintColor);
			empty.MouseFilter = MouseFilterEnum.Ignore;
			_shopStockList.AddChild(empty);
			return;
		}

		foreach (var item in MerchantStore.Stock)
			_shopStockList.AddChild(BuildStockItemRow(item));
	}

	Control BuildStockItemRow(EquippableItem item)
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 4);
		card.MouseFilter = MouseFilterEnum.Stop;
		card.MouseEntered += () => GameTooltip.Show(item.Name,
			$"{item.Rarity}  •  {EquipSlotControl.SlotDisplayName(item.Slot)}\n\n{item.Description}");
		card.MouseExited += () => GameTooltip.Hide();

		// Icon + name + price row
		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 10);
		card.AddChild(topRow);

		if (item.Icon != null)
		{
			var icon = new TextureRect();
			icon.Texture = item.Icon;
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.CustomMinimumSize = new Vector2(36f, 36f);
			topRow.AddChild(icon);
		}

		var nameCol = new VBoxContainer();
		nameCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		nameCol.AddThemeConstantOverride("separation", 2);
		topRow.AddChild(nameCol);

		var nameLabel = new Label();
		nameLabel.Text = item.Name;
		nameLabel.AddThemeFontSizeOverride("font_size", 14);
		nameLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameCol.AddChild(nameLabel);

		var price = MerchantStore.BuyPrice(item.Rarity);
		var priceLabel = new Label();
		priceLabel.Text = $"{price}g";
		priceLabel.AddThemeFontSizeOverride("font_size", 12);
		priceLabel.AddThemeColorOverride("font_color", GoldColor);
		priceLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameCol.AddChild(priceLabel);

		// Description
		if (!string.IsNullOrEmpty(item.Description))
		{
			var desc = new Label();
			desc.Text = item.Description;
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.AddThemeFontSizeOverride("font_size", 11);
			desc.AddThemeColorOverride("font_color", HintColor);
			desc.MouseFilter = MouseFilterEnum.Ignore;
			card.AddChild(desc);
		}

		// Buy button (or "Can't afford" hint)
		var canAfford = RunState.Instance.Gold >= price;
		var buyBtn = new Button();
		buyBtn.Text = $"Buy  ({price}g)";
		buyBtn.Disabled = !canAfford;
		buyBtn.AddThemeColorOverride("font_color", canAfford ? BuyColor : DisabledColor);
		var capturedItem = item;
		buyBtn.Pressed += () => OnBuyStockItem(capturedItem);
		card.AddChild(buyBtn);

		return card;
	}

	void OnBuyStockItem(EquippableItem item)
	{
		if (!MerchantStore.BuyStockItem(item)) return;
		Refresh();
	}

	// ── Stone of Rebirth row ──────────────────────────────────────────────

	Control BuildStoneOfRebirthRow()
	{
		var card = new VBoxContainer();
		card.AddThemeConstantOverride("separation", 6);

		// Icon + name + price row
		var topRow = new HBoxContainer();
		topRow.AddThemeConstantOverride("separation", 10);
		card.AddChild(topRow);

		var icon = new TextureRect();
		icon.Texture = GD.Load<Texture2D>(AssetConstants.StoneOfRebirthIconPath);
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.CustomMinimumSize = new Vector2(40f, 40f);
		topRow.AddChild(icon);

		var nameCol = new VBoxContainer();
		nameCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		nameCol.AddThemeConstantOverride("separation", 2);
		topRow.AddChild(nameCol);

		var nameLabel = new Label();
		nameLabel.Text = "Stone of Rebirth";
		nameLabel.AddThemeFontSizeOverride("font_size", 14);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.80f, 0.65f, 0.95f));
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameCol.AddChild(nameLabel);

		var priceLabel = new Label();
		priceLabel.Text = $"{MerchantStore.StoneOfRebirthPrice}g";
		priceLabel.AddThemeFontSizeOverride("font_size", 13);
		priceLabel.AddThemeColorOverride("font_color", GoldColor);
		priceLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameCol.AddChild(priceLabel);

		// Description
		var desc = new Label();
		desc.Text = "The next character who falls in battle is revived at 50% health.";
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.AddThemeFontSizeOverride("font_size", 12);
		desc.AddThemeColorOverride("font_color", HintColor);
		desc.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(desc);

		// Status / Buy button
		_stoneStatusLabel = new Label();
		_stoneStatusLabel.AddThemeFontSizeOverride("font_size", 12);
		_stoneStatusLabel.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(_stoneStatusLabel);

		_buyStoneButton = new Button();
		_buyStoneButton.Text = $"Buy  ({MerchantStore.StoneOfRebirthPrice}g)";
		_buyStoneButton.AddThemeColorOverride("font_color", BuyColor);
		_buyStoneButton.Pressed += OnBuyStone;
		card.AddChild(_buyStoneButton);

		RefreshStoneStatus();
		return card;
	}

	void RefreshStoneStatus()
	{
		if (_stoneStatusLabel == null || _buyStoneButton == null) return;

		if (MerchantStore.StoneOfRebirthConsumed)
		{
			_stoneStatusLabel.Text = "Consumed — the stone's magic was spent.";
			_stoneStatusLabel.AddThemeColorOverride("font_color", DimColor);
			_buyStoneButton.Visible = false;
		}
		else if (MerchantStore.StoneOfRebirthPurchased)
		{
			_stoneStatusLabel.Text = "✓ Active — waiting to trigger.";
			_stoneStatusLabel.AddThemeColorOverride("font_color", BuyColor);
			_buyStoneButton.Visible = false;
		}
		else
		{
			var canAfford = RunState.Instance.Gold >= MerchantStore.StoneOfRebirthPrice;
			_buyStoneButton.Visible = true;
			_buyStoneButton.Disabled = !canAfford;
			_buyStoneButton.AddThemeColorOverride("font_color", canAfford ? BuyColor : DisabledColor);
		}
	}

	void OnBuyStone()
	{
		if (!RunState.Instance.SpendGold(MerchantStore.StoneOfRebirthPrice)) return;
		MerchantStore.PurchaseStoneOfRebirth();
		Refresh();
	}

	// ── Sell section ──────────────────────────────────────────────────────

	Control BuildSellSection()
	{
		var vbox = new VBoxContainer();
		vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		vbox.AddThemeConstantOverride("separation", 8);

		// Header row with gold balance on the right
		var headerRow = new HBoxContainer();
		headerRow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		vbox.AddChild(headerRow);

		var header = new Label();
		header.Text = "Sell Items";
		header.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddThemeFontSizeOverride("font_size", 16);
		header.AddThemeColorOverride("font_color", TitleColor);
		header.MouseFilter = MouseFilterEnum.Ignore;
		headerRow.AddChild(header);

		_goldLabel = new Label();
		_goldLabel.AddThemeFontSizeOverride("font_size", 15);
		_goldLabel.AddThemeColorOverride("font_color", GoldColor);
		_goldLabel.HorizontalAlignment = HorizontalAlignment.Right;
		_goldLabel.MouseFilter = MouseFilterEnum.Ignore;
		headerRow.AddChild(_goldLabel);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		vbox.AddChild(scroll);

		_sellList = new VBoxContainer();
		_sellList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_sellList.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(_sellList);

		var hint = new Label();
		hint.Text = "Sell unwanted items from your inventory to earn gold.";
		hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		hint.AddThemeFontSizeOverride("font_size", 11);
		hint.AddThemeColorOverride("font_color", HintColor);
		hint.MouseFilter = MouseFilterEnum.Ignore;
		vbox.AddChild(hint);

		RefreshGoldLabel();
		RebuildSellList();
		return vbox;
	}

	void RefreshGoldLabel()
	{
		if (_goldLabel == null) return;
		_goldLabel.Text = $"Gold: {RunState.Instance.Gold}g";
	}

	void RebuildSellList()
	{
		if (_sellList == null) return;

		foreach (var child in _sellList.GetChildren())
			child.QueueFree();

		var inventory = ItemStore.Inventory;
		if (inventory.Count == 0)
		{
			var empty = new Label();
			empty.Text = "No items in inventory.";
			empty.AddThemeFontSizeOverride("font_size", 13);
			empty.AddThemeColorOverride("font_color", HintColor);
			empty.MouseFilter = MouseFilterEnum.Ignore;
			_sellList.AddChild(empty);
			return;
		}

		foreach (var item in inventory)
			_sellList.AddChild(BuildSellRow(item));
	}

	Control BuildSellRow(EquippableItem item)
	{
		var row = new HBoxContainer();
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddThemeConstantOverride("separation", 10);
		row.MouseFilter = MouseFilterEnum.Stop;
		row.MouseEntered += () => GameTooltip.Show(item.Name,
			$"{item.Rarity}  •  {EquipSlotControl.SlotDisplayName(item.Slot)}\n\n{item.Description}");
		row.MouseExited += () => GameTooltip.Hide();

		if (item.Icon != null)
		{
			var icon = new TextureRect();
			icon.Texture = item.Icon;
			icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			icon.CustomMinimumSize = new Vector2(32f, 32f);
			row.AddChild(icon);
		}

		var nameLabel = new Label();
		nameLabel.Text = item.Name;
		nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		nameLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		nameLabel.AddThemeColorOverride("font_color", RarityColor(item.Rarity));
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(nameLabel);

		var price = MerchantStore.SellPrice(item.Rarity);
		var priceLabel = new Label();
		priceLabel.Text = $"{price}g";
		priceLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		priceLabel.AddThemeFontSizeOverride("font_size", 13);
		priceLabel.AddThemeColorOverride("font_color", GoldColor);
		priceLabel.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(priceLabel);

		var sellBtn = new Button();
		sellBtn.Text = "Sell";
		sellBtn.AddThemeColorOverride("font_color", SellColor);
		var capturedItem = item;
		sellBtn.Pressed += () => OnSellItem(capturedItem);
		row.AddChild(sellBtn);

		return row;
	}

	void OnSellItem(EquippableItem item)
	{
		ItemStore.RemoveFromInventory(item);
		RunState.Instance.AddGold(MerchantStore.SellPrice(item.Rarity));
		Refresh();
	}

	// ── helpers ───────────────────────────────────────────────────────────

	static Color RarityColor(ItemRarity rarity)
	{
		return rarity switch
		{
			ItemRarity.Rare => new Color(0.40f, 0.60f, 0.95f),
			ItemRarity.Epic => new Color(0.70f, 0.35f, 0.90f),
			ItemRarity.Legendary => new Color(0.95f, 0.65f, 0.10f),
			_ => new Color(0.70f, 0.65f, 0.60f)
		};
	}
}
#nullable enable
using System;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.Merchant;

namespace healerfantasy.UI;

/// <summary>
/// The Merchant's shop panel, opened by clicking the Merchant interactible in Camp.
///
/// Layout:
///   "Ah, a weary Lightkeeper! …"                              [◉ 145 Gold]
///   ┌─ The Merchant's Wares ─────────┐   ┌─ Your Belongings ─────────────────┐
///   │ ┌────┐ Stone of Rebirth  [70g] │   │ CONSUMABLES                       │
///   │ │icon│ Consumable        [Buy] │   │  [icon] Stone of Rebirth  ACTIVE  │
///   │ └────┘ "The next character…"   │   │ EQUIPPED                          │
///   │ ┌────┐ Crystal Staff     [30g] │   │  [icon] Ruler's Signet  EQUIPPED  │
///   │ │icon│ Rare • Staff      [Buy] │   │ INVENTORY                         │
///   │ └────┘ "…"                     │   │  [icon] Seer's Band   [Sell 20g]  │
///   └────────────────────────────────┘   └───────────────────────────────────┘
///
/// Buy:  deducts gold, moves item to player inventory (or activates the stone).
///       The purchase appears in Your Belongings with a "NEW" tag.
/// Sell: removes item from player inventory, adds gold.
/// </summary>
public partial class MerchantPane : Control
{
	// ── colour palette ────────────────────────────────────────────────────
	static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
	static readonly Color HintColor = new(0.55f, 0.51f, 0.46f);
	static readonly Color SepColor = new(0.50f, 0.40f, 0.22f, 0.55f);
	static readonly Color GoldColor = new(0.98f, 0.82f, 0.32f);
	static readonly Color DimColor = new(0.40f, 0.38f, 0.35f);
	static readonly Color BuyColor = new(0.45f, 0.85f, 0.50f);
	static readonly Color SellColor = new(0.95f, 0.72f, 0.30f);
	static readonly Color TooExpensiveColor = new(0.85f, 0.35f, 0.30f);
	static readonly Color StoneColor = new(0.80f, 0.65f, 0.95f);
	static readonly Color NewColor = new(0.95f, 0.84f, 0.50f);

	// Section frames: the wares side is lit warmer (the merchant's lantern),
	// the belongings side is cooler and darker.
	static readonly Color WaresBg = new(0.13f, 0.09f, 0.06f, 0.95f);
	static readonly Color WaresBorder = new(0.62f, 0.46f, 0.22f, 0.85f);
	static readonly Color BelongingsBg = new(0.08f, 0.08f, 0.09f, 0.95f);
	static readonly Color BelongingsBorder = new(0.42f, 0.38f, 0.32f, 0.75f);
	static readonly Color CardBg = new(0.05f, 0.04f, 0.04f, 0.65f);

	const string StoneDescription = "The next character who falls in battle is revived at 50% light.";

	// Live-updated controls
	Label _goldLabel = null!;
	VBoxContainer _waresList = null!;
	VBoxContainer _belongingsList = null!;

	// The most recent purchase, tagged "NEW" in Your Belongings so the player
	// can see where it went. Null = nothing / the stone (see _stoneJustBought).
	EquippableItem? _justBought;
	bool _stoneJustBought;

	public override void _Ready()
	{
		SizeFlagsHorizontal = SizeFlags.ExpandFill;
		SizeFlagsVertical = SizeFlags.ExpandFill;

		var outer = new MarginContainer();
		outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		outer.AddThemeConstantOverride("margin_left", 12);
		outer.AddThemeConstantOverride("margin_right", 12);
		outer.AddThemeConstantOverride("margin_top", 4);
		outer.AddThemeConstantOverride("margin_bottom", 6);
		AddChild(outer);

		var root = new VBoxContainer();
		root.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		root.SizeFlagsVertical = SizeFlags.ExpandFill;
		root.AddThemeConstantOverride("separation", 12);
		outer.AddChild(root);

		root.AddChild(BuildHeaderRow());

		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 18);
		root.AddChild(hbox);

		hbox.AddChild(BuildSection("The Merchant's Wares", "Buy", WaresBg, WaresBorder, out _waresList));
		hbox.AddChild(BuildSection("Your Belongings", "Sell", BelongingsBg, BelongingsBorder,
			out _belongingsList));

		Refresh();
	}

	// ── public API ────────────────────────────────────────────────────────

	/// <summary>
	/// Sync the panel with current RunState / MerchantStore / ItemStore state.
	/// Called each time the panel is opened.
	/// </summary>
	public void Refresh()
	{
		if (_goldLabel == null) return;
		_goldLabel.Text = $"{RunState.Instance.Gold} Gold";
		RebuildWares();
		RebuildBelongings();
	}

	/// <summary>Clears the "NEW" tag — call when the shop is (re)opened.</summary>
	public void ClearRecentPurchase()
	{
		_justBought = null;
		_stoneJustBought = false;
	}

	// ── layout builders ───────────────────────────────────────────────────

	Control BuildHeaderRow()
	{
		var row = new HBoxContainer();
		row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		row.AddThemeConstantOverride("separation", 16);

		var quote = new Label();
		quote.Text = "\"Ah, a weary Lightkeeper! Fine wares for the discerning adventurer...\"";
		quote.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		quote.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		quote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		quote.AddThemeFontSizeOverride("font_size", 14);
		quote.AddThemeColorOverride("font_color", HintColor);
		quote.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(quote);

		// Gold purse — the player's balance, always visible top-right.
		var purseStyle = new StyleBoxFlat();
		purseStyle.BgColor = new Color(0.16f, 0.12f, 0.05f, 0.95f);
		purseStyle.SetBorderWidthAll(2);
		purseStyle.BorderColor = new Color(GoldColor, 0.75f);
		purseStyle.SetCornerRadiusAll(18);
		purseStyle.ContentMarginLeft = 10f;
		purseStyle.ContentMarginRight = 16f;
		purseStyle.ContentMarginTop = purseStyle.ContentMarginBottom = 4f;

		var purse = new PanelContainer();
		purse.AddThemeStyleboxOverride("panel", purseStyle);
		purse.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		purse.MouseFilter = MouseFilterEnum.Stop;
		purse.MouseEntered += () => GameTooltip.Show("Your Gold",
			"Earned from defeating bosses and selling items to the Merchant.");
		purse.MouseExited += () => GameTooltip.Hide();
		row.AddChild(purse);

		var purseRow = new HBoxContainer();
		purseRow.AddThemeConstantOverride("separation", 8);
		purseRow.MouseFilter = MouseFilterEnum.Ignore;
		purse.AddChild(purseRow);

		var coin = new TextureRect();
		coin.Texture = GD.Load<Texture2D>(AssetConstants.GoldIconPath);
		coin.CustomMinimumSize = new Vector2(28f, 28f);
		coin.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		coin.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		coin.MouseFilter = MouseFilterEnum.Ignore;
		purseRow.AddChild(coin);

		_goldLabel = new Label();
		_goldLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		_goldLabel.AddThemeFontSizeOverride("font_size", 20);
		_goldLabel.AddThemeColorOverride("font_color", GoldColor);
		_goldLabel.MouseFilter = MouseFilterEnum.Ignore;
		purseRow.AddChild(_goldLabel);

		return row;
	}

	/// <summary>A framed, titled column with a scrollable list inside.</summary>
	static Control BuildSection(string title, string tag, Color bg, Color border, out VBoxContainer list)
	{
		var style = new StyleBoxFlat();
		style.BgColor = bg;
		style.SetBorderWidthAll(2);
		style.BorderColor = border;
		style.SetCornerRadiusAll(6);
		style.ContentMarginLeft = style.ContentMarginRight = 14f;
		style.ContentMarginTop = 10f;
		style.ContentMarginBottom = 12f;

		var frame = new PanelContainer();
		frame.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		frame.SizeFlagsVertical = SizeFlags.ExpandFill;
		frame.SizeFlagsStretchRatio = 1f;
		frame.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 8);
		frame.AddChild(vbox);

		var headerRow = new HBoxContainer();
		vbox.AddChild(headerRow);

		var header = new Label();
		header.Text = title;
		header.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		header.AddThemeFontSizeOverride("font_size", 18);
		header.AddThemeColorOverride("font_color", TitleColor);
		headerRow.AddChild(header);

		var tagLabel = new Label();
		tagLabel.Text = tag.ToUpperInvariant();
		tagLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		tagLabel.AddThemeFontSizeOverride("font_size", 11);
		tagLabel.AddThemeColorOverride("font_color", new Color(border, 0.9f));
		headerRow.AddChild(tagLabel);

		var sep = new HSeparator();
		sep.AddThemeColorOverride("color", SepColor);
		vbox.AddChild(sep);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
		scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
		vbox.AddChild(scroll);

		list = new VBoxContainer();
		list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		list.AddThemeConstantOverride("separation", 8);
		scroll.AddChild(list);

		return frame;
	}

	// ── wares (buy) ───────────────────────────────────────────────────────

	void RebuildWares()
	{
		ClearChildren(_waresList);

		// Stone of Rebirth — a permanent fixture, listed like any other ware.
		var stoneIcon = GD.Load<Texture2D>(AssetConstants.StoneOfRebirthIconPath);
		if (MerchantStore.StoneOfRebirthPurchased)
		{
			_waresList.AddChild(BuildCard(stoneIcon, "Stone of Rebirth", StoneColor, "Consumable",
				StoneDescription, BuildStatusTag("SOLD", DimColor), dimmed: true));
		}
		else
		{
			_waresList.AddChild(BuildCard(stoneIcon, "Stone of Rebirth", StoneColor,
				"Consumable  •  One use", StoneDescription,
				BuildBuyControls(MerchantStore.StoneOfRebirthPrice, OnBuyStone)));
		}

		if (MerchantStore.Stock.Count == 0)
		{
			_waresList.AddChild(BuildHint("No equipment in stock today — come back after the next dungeon."));
			return;
		}

		foreach (var item in MerchantStore.Stock)
		{
			var captured = item;
			var card = BuildCard(item.Icon, item.Name, RarityColor(item.Rarity), ItemSubtitle(item),
				item.Description, BuildBuyControls(MerchantStore.BuyPrice(item.Rarity),
					() => OnBuyStockItem(captured)));
			AttachItemTooltip(card, item);
			_waresList.AddChild(card);
		}
	}

	Control BuildBuyControls(int price, Action onBuy)
	{
		var canAfford = RunState.Instance.Gold >= price;

		var col = new VBoxContainer();
		col.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		col.AddThemeConstantOverride("separation", 4);

		var priceLabel = new Label();
		priceLabel.Text = $"{price} Gold";
		priceLabel.HorizontalAlignment = HorizontalAlignment.Center;
		priceLabel.AddThemeFontSizeOverride("font_size", 14);
		priceLabel.AddThemeColorOverride("font_color", canAfford ? GoldColor : TooExpensiveColor);
		priceLabel.MouseFilter = MouseFilterEnum.Ignore;
		col.AddChild(priceLabel);

		var btn = BuildActionButton(canAfford ? "Buy" : "Can't afford", BuyColor, !canAfford);
		btn.Pressed += onBuy;
		col.AddChild(btn);

		return col;
	}

	void OnBuyStockItem(EquippableItem item)
	{
		if (!MerchantStore.BuyStockItem(item)) return;
		GameTooltip.Hide();
		_justBought = item;
		_stoneJustBought = false;
		Refresh();
	}

	void OnBuyStone()
	{
		if (!RunState.Instance.SpendGold(MerchantStore.StoneOfRebirthPrice)) return;
		MerchantStore.PurchaseStoneOfRebirth();
		_justBought = null;
		_stoneJustBought = true;
		Refresh();
	}

	// ── belongings (sell) ─────────────────────────────────────────────────

	void RebuildBelongings()
	{
		ClearChildren(_belongingsList);

		// Consumables
		if (MerchantStore.StoneOfRebirthPurchased)
		{
			_belongingsList.AddChild(BuildSubheader("Consumables"));
			var icon = GD.Load<Texture2D>(AssetConstants.StoneOfRebirthIconPath);
			var status = MerchantStore.StoneOfRebirthConsumed
				? BuildStatusTag("SPENT", DimColor)
				: BuildStatusTag("ACTIVE", BuyColor);
			var subtitle = MerchantStore.StoneOfRebirthConsumed
				? "Its magic was spent reviving an ally."
				: "Revives the next ally who falls.";
			_belongingsList.AddChild(BuildCard(icon, "Stone of Rebirth", StoneColor, subtitle, null,
				status, dimmed: MerchantStore.StoneOfRebirthConsumed, isNew: _stoneJustBought,
				compact: true));
		}

		// Equipped — shown so the player can compare against wares; not sellable.
		var equippedAny = false;
		foreach (EquipSlot slot in Enum.GetValues(typeof(EquipSlot)))
		{
			var item = ItemStore.GetEquipped(slot);
			if (item == null) continue;
			if (!equippedAny) _belongingsList.AddChild(BuildSubheader("Equipped"));
			equippedAny = true;

			var card = BuildCard(item.Icon, item.Name, RarityColor(item.Rarity), ItemSubtitle(item), null,
				BuildStatusTag("EQUIPPED", HintColor), compact: true);
			AttachItemTooltip(card, item, "\n\nUnequip it in the Armory to sell it.");
			_belongingsList.AddChild(card);
		}

		// Inventory — sellable.
		_belongingsList.AddChild(BuildSubheader("Inventory"));
		if (ItemStore.Inventory.Count == 0)
		{
			_belongingsList.AddChild(BuildHint("Nothing to sell. Unequipped items appear here."));
			return;
		}

		foreach (var item in ItemStore.Inventory)
		{
			var captured = item;
			var price = MerchantStore.SellPrice(item.Rarity);
			var btn = BuildActionButton($"Sell  +{price}", SellColor, false);
			btn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			btn.Pressed += () => OnSellItem(captured);

			var card = BuildCard(item.Icon, item.Name, RarityColor(item.Rarity), ItemSubtitle(item), null,
				btn, isNew: ReferenceEquals(item, _justBought), compact: true);
			AttachItemTooltip(card, item);
			_belongingsList.AddChild(card);
		}
	}

	void OnSellItem(EquippableItem item)
	{
		GameTooltip.Hide();
		ItemStore.RemoveFromInventory(item);
		RunState.Instance.AddGold(MerchantStore.SellPrice(item.Rarity));
		if (ReferenceEquals(item, _justBought)) _justBought = null;
		Refresh();
	}

	// ── card building blocks ──────────────────────────────────────────────

	/// <summary>
	/// One row: framed icon, name / subtitle / optional description, and a
	/// right-hand action control (buy controls, sell button or status tag).
	/// </summary>
	static Control BuildCard(Texture2D? icon, string name, Color accent, string subtitle, string? description,
		Control action, bool dimmed = false, bool isNew = false, bool compact = false)
	{
		var style = new StyleBoxFlat();
		style.BgColor = isNew ? new Color(0.20f, 0.15f, 0.06f, 0.85f) : CardBg;
		style.SetBorderWidthAll(1);
		style.BorderWidthLeft = 3;
		style.BorderColor = isNew ? NewColor : new Color(accent, dimmed ? 0.25f : 0.55f);
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = 8f;
		style.ContentMarginRight = 10f;
		style.ContentMarginTop = style.ContentMarginBottom = compact ? 5f : 8f;

		var card = new PanelContainer();
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.AddThemeStyleboxOverride("panel", style);
		card.MouseFilter = MouseFilterEnum.Stop;
		if (dimmed) card.Modulate = new Color(1f, 1f, 1f, 0.6f);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		card.AddChild(row);

		row.AddChild(BuildIconFrame(icon, accent, compact ? 40f : 56f));

		var textCol = new VBoxContainer();
		textCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		textCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		textCol.AddThemeConstantOverride("separation", 2);
		textCol.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(textCol);

		var nameRow = new HBoxContainer();
		nameRow.AddThemeConstantOverride("separation", 8);
		nameRow.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(nameRow);

		var nameLabel = new Label();
		nameLabel.Text = name;
		nameLabel.AddThemeFontSizeOverride("font_size", compact ? 14 : 16);
		nameLabel.AddThemeColorOverride("font_color", accent);
		nameLabel.MouseFilter = MouseFilterEnum.Ignore;
		nameRow.AddChild(nameLabel);

		if (isNew)
		{
			var newLabel = new Label();
			newLabel.Text = "NEW";
			newLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			newLabel.AddThemeFontSizeOverride("font_size", 10);
			newLabel.AddThemeColorOverride("font_color", NewColor);
			newLabel.MouseFilter = MouseFilterEnum.Ignore;
			nameRow.AddChild(newLabel);
		}

		var subLabel = new Label();
		subLabel.Text = subtitle;
		subLabel.AddThemeFontSizeOverride("font_size", 11);
		subLabel.AddThemeColorOverride("font_color", HintColor);
		subLabel.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(subLabel);

		if (!string.IsNullOrEmpty(description))
		{
			var desc = new Label();
			desc.Text = description;
			desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			desc.CustomMinimumSize = new Vector2(120f, 0f);
			desc.AddThemeFontSizeOverride("font_size", 12);
			desc.AddThemeColorOverride("font_color", new Color(0.72f, 0.68f, 0.62f));
			desc.MouseFilter = MouseFilterEnum.Ignore;
			textCol.AddChild(desc);
		}

		action.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		row.AddChild(action);

		return card;
	}

	static Control BuildIconFrame(Texture2D? icon, Color accent, float size)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.03f, 0.03f, 0.03f, 0.9f);
		style.SetBorderWidthAll(2);
		style.BorderColor = accent;
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = style.ContentMarginRight = 4f;
		style.ContentMarginTop = style.ContentMarginBottom = 4f;

		var frame = new PanelContainer();
		frame.CustomMinimumSize = new Vector2(size, size);
		frame.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		frame.AddThemeStyleboxOverride("panel", style);
		frame.MouseFilter = MouseFilterEnum.Ignore;

		var tex = new TextureRect();
		tex.Texture = icon;
		tex.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		tex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		tex.MouseFilter = MouseFilterEnum.Ignore;
		frame.AddChild(tex);

		return frame;
	}

	static Button BuildActionButton(string text, Color color, bool disabled)
	{
		StyleBoxFlat MakeStyle(float bgAlpha, float borderAlpha)
		{
			var s = new StyleBoxFlat();
			s.BgColor = new Color(color.R * 0.25f, color.G * 0.25f, color.B * 0.25f, bgAlpha);
			s.SetBorderWidthAll(1);
			s.BorderColor = new Color(color, borderAlpha);
			s.SetCornerRadiusAll(4);
			s.ContentMarginLeft = s.ContentMarginRight = 12f;
			s.ContentMarginTop = s.ContentMarginBottom = 4f;
			return s;
		}

		var btn = new Button();
		btn.Text = text;
		btn.Disabled = disabled;
		btn.CustomMinimumSize = new Vector2(96f, 0f);
		btn.FocusMode = FocusModeEnum.None;
		btn.MouseDefaultCursorShape = disabled ? CursorShape.Arrow : CursorShape.PointingHand;
		btn.AddThemeFontSizeOverride("font_size", 13);
		btn.AddThemeColorOverride("font_color", color);
		btn.AddThemeColorOverride("font_hover_color", color.Lightened(0.3f));
		btn.AddThemeColorOverride("font_disabled_color", DimColor);
		btn.AddThemeStyleboxOverride("normal", MakeStyle(0.8f, 0.7f));
		btn.AddThemeStyleboxOverride("hover", MakeStyle(1f, 1f));
		btn.AddThemeStyleboxOverride("pressed", MakeStyle(1f, 1f));
		var disabledStyle = MakeStyle(0.4f, 0.2f);
		disabledStyle.BgColor = new Color(0.08f, 0.08f, 0.08f, 0.6f);
		disabledStyle.BorderColor = new Color(DimColor, 0.4f);
		btn.AddThemeStyleboxOverride("disabled", disabledStyle);
		return btn;
	}

	static Control BuildStatusTag(string text, Color color)
	{
		var label = new Label();
		label.Text = text;
		label.CustomMinimumSize = new Vector2(96f, 0f);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", color);
		label.MouseFilter = MouseFilterEnum.Ignore;
		return label;
	}

	static Control BuildSubheader(string text)
	{
		var label = new Label();
		label.Text = text.ToUpperInvariant();
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", new Color(0.70f, 0.58f, 0.36f));
		label.MouseFilter = MouseFilterEnum.Ignore;
		return label;
	}

	static Control BuildHint(string text)
	{
		var label = new Label();
		label.Text = text;
		label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		label.AddThemeFontSizeOverride("font_size", 12);
		label.AddThemeColorOverride("font_color", HintColor);
		label.MouseFilter = MouseFilterEnum.Ignore;
		return label;
	}

	// ── helpers ───────────────────────────────────────────────────────────

	static void AttachItemTooltip(Control card, EquippableItem item, string extra = "")
	{
		card.MouseEntered += () => GameTooltip.Show(item.Name,
			$"{item.Rarity}  •  {EquipSlotControl.SlotDisplayName(item.Slot)}\n\n{item.Description}{extra}");
		card.MouseExited += () => GameTooltip.Hide();
	}

	static string ItemSubtitle(EquippableItem item) =>
		$"{item.Rarity}  •  {EquipSlotControl.SlotDisplayName(item.Slot)}";

	static void ClearChildren(Node node)
	{
		// Detach immediately so the old rows don't linger for a frame next to the new ones.
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	static Color RarityColor(ItemRarity rarity) => EquipSlotControl.RarityColor(rarity);
}

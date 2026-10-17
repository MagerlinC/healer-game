#nullable enable
using System;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources;
using healerfantasy.SpellResources.Umbra;
using healerfantasy.Talents;

namespace healerfantasy.UI;

/// <summary>
/// The Camp Armory panel: the Lightkeeper's gear and the talents earned this run.
/// Styled after <see cref="MerchantPane"/> (framed sections holding cards).
///
/// Layout:
///   "Your gear, laid out by the fire..."
///   ┌─ Equipped ─────────┐ ┌─ Inventory ───────────┐ ┌─ Talents ──────────────────┐
///   │ [slot] STAFF       │ │ [tile] [tile] [tile]  │ │ UMBRA  ●●○  Ultimate 2/3   │
///   │        Crystal Sta…│ │                       │ │  [icon] Umbral Specialist  │
///   │        "…"         │ │                       │ │         "20% increased…"   │
///   │ [slot] RING        │ │ ┄ drag here to unequip│ │ GENERAL                    │
///   └────────────────────┘ └───────────────────────┘ └────────────────────────────┘
///
/// Equipping uses the same drag/right-click controls as <see cref="EquipmentPane"/>
/// (<see cref="EquipSlotControl"/>, <see cref="InventoryItemControl"/>).
/// Talents are read-only here — they are earned on the victory screen.
/// </summary>
public partial class ArmoryPane : Control
{
	// ── colour palette (shared with MerchantPane) ─────────────────────────
	static readonly Color HintColor = new(0.55f, 0.51f, 0.46f);
	static readonly Color DescColor = new(0.72f, 0.68f, 0.62f);
	static readonly Color CardBg = new(0.05f, 0.04f, 0.04f, 0.65f);
	static readonly Color EmptyAccent = new(0.40f, 0.38f, 0.35f);

	// Equipped is lit warm (the fire), inventory cool, talents a faint violet glow.
	static readonly Color EquippedBg = new(0.13f, 0.09f, 0.06f, 0.95f);
	static readonly Color EquippedBorder = new(0.62f, 0.46f, 0.22f, 0.85f);
	static readonly Color InventoryBg = new(0.08f, 0.08f, 0.09f, 0.95f);
	static readonly Color InventoryBorder = new(0.42f, 0.38f, 0.32f, 0.75f);
	static readonly Color TalentsBg = new(0.09f, 0.07f, 0.11f, 0.95f);
	static readonly Color TalentsBorder = new(0.52f, 0.44f, 0.62f, 0.80f);

	const int UltimatePoints = 3;

	// Live references for incremental refresh. Slot controls are permanent (they
	// may be mid-drop when Refresh runs); only their label text is updated.
	EquipSlotControl[] _slotControls = Array.Empty<EquipSlotControl>();
	Label[] _itemNameLabels = Array.Empty<Label>();
	Label[] _itemDescLabels = Array.Empty<Label>();
	StyleBoxFlat[] _slotCardStyles = Array.Empty<StyleBoxFlat>();
	VBoxContainer _equippedList = null!;
	HFlowContainer _inventoryFlow = null!;
	Control _inventoryEmptyHint = null!;
	VBoxContainer _talentList = null!;

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

		var quote = new Label();
		quote.Text = "The items acquired and lessons learned from the dark so far.";
		quote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		quote.AddThemeFontSizeOverride("font_size", 14);
		quote.AddThemeColorOverride("font_color", HintColor);
		quote.MouseFilter = MouseFilterEnum.Ignore;
		root.AddChild(quote);

		var hbox = new HBoxContainer();
		hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.SizeFlagsVertical = SizeFlags.ExpandFill;
		hbox.AddThemeConstantOverride("separation", 18);
		root.AddChild(hbox);

		var equipped = MerchantPane.BuildSection("Equipped", "Worn", EquippedBg, EquippedBorder,
			out _equippedList);
		equipped.SizeFlagsStretchRatio = 1.1f;
		hbox.AddChild(equipped);

		var inventory = MerchantPane.BuildSection("Inventory", "Stash", InventoryBg, InventoryBorder,
			out var inventoryList, BuildInventoryFooter());
		inventory.SizeFlagsStretchRatio = 0.9f;
		hbox.AddChild(inventory);

		var talents = MerchantPane.BuildSection("Talents", "This run", TalentsBg, TalentsBorder,
			out _talentList);
		talents.SizeFlagsStretchRatio = 1.2f;
		hbox.AddChild(talents);

		BuildSlotCards();

		_inventoryFlow = new HFlowContainer();
		_inventoryFlow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		_inventoryFlow.AddThemeConstantOverride("h_separation", 8);
		_inventoryFlow.AddThemeConstantOverride("v_separation", 8);
		inventoryList.AddChild(_inventoryFlow);

		// Lives in the list rather than the flow: a flow container squeezes children
		// to their minimum width, which wraps an autowrapping label to one letter per line.
		_inventoryEmptyHint = MerchantPane.BuildHint(
			"No spare items.\nItems drop from defeated bosses, or can be bought from the Merchant.");
		inventoryList.AddChild(_inventoryEmptyHint);

		Refresh();
	}

	// ── public API ────────────────────────────────────────────────────────

	/// <summary>
	/// Sync the panel with ItemStore and the run's talents.
	/// Called each time the panel is opened, and after every equip change.
	/// </summary>
	public void Refresh()
	{
		if (_talentList == null) return;
		RefreshSlotCards();
		RebuildInventoryFlow();
		RebuildTalents();
	}

	// ── equipped (slot cards) ─────────────────────────────────────────────

	void BuildSlotCards()
	{
		var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));
		_slotControls = new EquipSlotControl[slots.Length];
		_itemNameLabels = new Label[slots.Length];
		_itemDescLabels = new Label[slots.Length];
		_slotCardStyles = new StyleBoxFlat[slots.Length];

		for (var i = 0; i < slots.Length; i++)
		{
			var style = new StyleBoxFlat();
			style.BgColor = CardBg;
			style.SetBorderWidthAll(1);
			style.BorderWidthLeft = 3;
			style.SetCornerRadiusAll(4);
			style.ContentMarginLeft = 8f;
			style.ContentMarginRight = 10f;
			style.ContentMarginTop = style.ContentMarginBottom = 6f;
			_slotCardStyles[i] = style;

			var card = new PanelContainer();
			card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			card.AddThemeStyleboxOverride("panel", style);
			card.MouseFilter = MouseFilterEnum.Ignore;
			_equippedList.AddChild(card);

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 12);
			row.MouseFilter = MouseFilterEnum.Ignore;
			card.AddChild(row);

			var slotCtrl = new EquipSlotControl(slots[i]);
			slotCtrl.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			slotCtrl.OnChanged = Refresh;
			_slotControls[i] = slotCtrl;
			row.AddChild(slotCtrl);

			var textCol = new VBoxContainer();
			textCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
			textCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
			textCol.AddThemeConstantOverride("separation", 2);
			textCol.MouseFilter = MouseFilterEnum.Ignore;
			row.AddChild(textCol);

			textCol.AddChild(MerchantPane.BuildSubheader(EquipSlotControl.SlotDisplayName(slots[i])));

			var nameLabel = new Label();
			nameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			nameLabel.AddThemeFontSizeOverride("font_size", 15);
			nameLabel.MouseFilter = MouseFilterEnum.Ignore;
			textCol.AddChild(nameLabel);
			_itemNameLabels[i] = nameLabel;

			var descLabel = new Label();
			descLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			descLabel.CustomMinimumSize = new Vector2(120f, 0f);
			descLabel.AddThemeFontSizeOverride("font_size", 12);
			descLabel.AddThemeColorOverride("font_color", DescColor);
			descLabel.MouseFilter = MouseFilterEnum.Ignore;
			textCol.AddChild(descLabel);
			_itemDescLabels[i] = descLabel;
		}
	}

	void RefreshSlotCards()
	{
		var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));
		for (var i = 0; i < slots.Length; i++)
		{
			_slotControls[i].Refresh();

			var item = ItemStore.GetEquipped(slots[i]);
			var accent = item != null ? EquipSlotControl.RarityColor(item.Rarity) : EmptyAccent;

			_slotCardStyles[i].BorderColor = new Color(accent, item != null ? 0.55f : 0.25f);
			_itemNameLabels[i].Text = item?.Name ?? "— Empty —";
			_itemNameLabels[i].AddThemeColorOverride("font_color", accent);
			_itemDescLabels[i].Text = item?.Description ?? "Drag an item from your inventory here.";
			_itemDescLabels[i].AddThemeColorOverride("font_color", item != null ? DescColor : HintColor);
		}
	}

	// ── inventory ─────────────────────────────────────────────────────────

	Control BuildInventoryFooter()
	{
		var col = new VBoxContainer();
		col.AddThemeConstantOverride("separation", 6);

		var dropZone = new InventoryDropZoneControl();
		dropZone.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		dropZone.CustomMinimumSize = new Vector2(0f, 34f);
		dropZone.OnChanged = Refresh;
		col.AddChild(dropZone);

		var hint = MerchantPane.BuildHint(
			"• Drag items to their slot to equip (or right-click)\n• Drag slots here to unequip (or right-click)");
		hint.AddThemeFontSizeOverride("font_size", 11);
		col.AddChild(hint);

		return col;
	}

	void RebuildInventoryFlow()
	{
		MerchantPane.ClearChildren(_inventoryFlow);
		_inventoryEmptyHint.Visible = ItemStore.Inventory.Count == 0;

		foreach (var item in ItemStore.Inventory)
		{
			var ctrl = new InventoryItemControl(item);
			ctrl.OnUnequipDrop = Refresh;
			ctrl.OnQuickEquip = Refresh;
			_inventoryFlow.AddChild(ctrl);
		}
	}

	// ── talents ───────────────────────────────────────────────────────────

	void RebuildTalents()
	{
		MerchantPane.ClearChildren(_talentList);

		var acquired = RunState.Instance.SelectedTalentDefs;
		if (acquired.Count == 0)
		{
			_talentList.AddChild(MerchantPane.BuildHint(
				"No talents yet.\nEach boss you defeat lets you choose one. " +
				$"Gather {UltimatePoints} from one school to unlock its Ultimate."));
			return;
		}

		foreach (var school in TalentSchoolOrder)
		{
			var schoolTalents = acquired.Where(t => t.School == school).ToList();
			if (schoolTalents.Count == 0) continue;

			_talentList.AddChild(BuildSchoolHeader(school, schoolTalents.Count));
			foreach (var def in schoolTalents)
				_talentList.AddChild(BuildTalentCard(def));
		}
	}

	/// <summary>School name in its colour, with pips counting toward the school's Ultimate.</summary>
	static Control BuildSchoolHeader(SpellSchool school, int count)
	{
		var accent = SchoolInfo.Color(school);

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		row.MouseFilter = MouseFilterEnum.Ignore;

		var name = new Label();
		name.Text = SchoolInfo.Name(school).ToUpperInvariant();
		name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		name.AddThemeFontSizeOverride("font_size", 12);
		name.AddThemeColorOverride("font_color", accent);
		name.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(name);

		var line = new HSeparator();
		line.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		line.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		line.AddThemeColorOverride("color", new Color(accent, 0.30f));
		line.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(line);

		var ultimate = SpellRegistry.AllSpells.OfType<UltimateSpellResource>()
			.FirstOrDefault(u => u.School == school);
		if (ultimate == null)
		{
			row.AddChild(BuildTag(count.ToString(), new Color(accent, 0.8f)));
			return row;
		}

		var required = Math.Max(1, ultimate.RequiredSchoolPoints);
		var unlocked = count >= required;

		var pips = new Label();
		pips.Text = string.Concat(Enumerable.Range(0, required).Select(i => i < count ? "●" : "○"));
		pips.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		pips.AddThemeFontSizeOverride("font_size", 15);
		pips.AddThemeColorOverride("font_color", accent);
		pips.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(pips);

		var tag = BuildTag(unlocked ? "ULTIMATE READY" : $"{Math.Min(count, required)}/{required}",
			unlocked ? accent : HintColor);
		tag.MouseFilter = MouseFilterEnum.Stop;
		tag.MouseEntered += () => GameTooltip.Show($"{ultimate.Name} (Ultimate)",
			unlocked
				? $"Unlocked — equip it from the Spellbook.\n\n{ultimate.Description}"
				: $"Acquire {required} {SchoolInfo.Name(school)} talents to unlock.\n\n{ultimate.Description}");
		tag.MouseExited += () => GameTooltip.Hide();
		row.AddChild(tag);

		return row;
	}

	static Control BuildTalentCard(TalentDefinition def)
	{
		var accent = SchoolInfo.Color(def.School);

		var style = new StyleBoxFlat();
		style.BgColor = CardBg;
		style.SetBorderWidthAll(1);
		style.BorderWidthLeft = 3;
		style.BorderColor = new Color(accent, 0.55f);
		style.SetCornerRadiusAll(4);
		style.ContentMarginLeft = 8f;
		style.ContentMarginRight = 10f;
		style.ContentMarginTop = style.ContentMarginBottom = 5f;

		var card = new PanelContainer();
		card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		card.AddThemeStyleboxOverride("panel", style);
		card.MouseFilter = MouseFilterEnum.Ignore;

		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 12);
		row.MouseFilter = MouseFilterEnum.Ignore;
		card.AddChild(row);

		row.AddChild(MerchantPane.BuildIconFrame(def.LoadIcon(), new Color(accent, 0.8f), 44f));

		var textCol = new VBoxContainer();
		textCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		textCol.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		textCol.AddThemeConstantOverride("separation", 2);
		textCol.MouseFilter = MouseFilterEnum.Ignore;
		row.AddChild(textCol);

		var name = new Label();
		name.Text = def.Name;
		name.AddThemeFontSizeOverride("font_size", 14);
		name.AddThemeColorOverride("font_color", accent);
		name.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(name);

		var desc = new Label();
		desc.Text = def.Description;
		desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		desc.CustomMinimumSize = new Vector2(120f, 0f);
		desc.AddThemeFontSizeOverride("font_size", 12);
		desc.AddThemeColorOverride("font_color", DescColor);
		desc.MouseFilter = MouseFilterEnum.Ignore;
		textCol.AddChild(desc);

		return card;
	}

	static Label BuildTag(string text, Color color)
	{
		var label = new Label();
		label.Text = text;
		label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
		label.AddThemeFontSizeOverride("font_size", 11);
		label.AddThemeColorOverride("font_color", color);
		label.MouseFilter = MouseFilterEnum.Ignore;
		return label;
	}

	// Matches the talent board's grouping (LoadoutController.TalentSchoolOrder).
	static readonly SpellSchool[] TalentSchoolOrder =
	{
		SpellSchool.Generic, SpellSchool.Wick, SpellSchool.Flame,
		SpellSchool.Ember, SpellSchool.Umbra, SpellSchool.Pyre
	};
}
#nullable enable
using System;
using Godot;
using healerfantasy.Items;

namespace healerfantasy.UI;

/// <summary>
/// Reusable equipment-management panel used by both the Armory and Victory Screen.
///
/// Layout (left → right):
///   ┌─ Equipment ──────┐  │  ┌─ Inventory ─────────────────────────────┐
///   │ [slot] Staff     │  │  │  [icon] [icon] [icon]  ← draggable tiles │
///   │ [slot] Ring      │  │  │  ┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄   │
///   │ [slot] Amulet    │  │  │  ↑ Drag equipped items here to unequip   │
///   └──────────────────┘  │  └─────────────────────────────────────────┘
///
/// Drag-and-drop rules (enforced by EquipSlotControl / InventoryItemControl):
///   • Inventory → Slot  : equips item (only accepted by the matching slot type).
///   • Slot → Inventory  : unequips item (drop on an inventory tile or the strip).
///
/// Pass <paramref name="highlightItem"/> to mark a freshly-dropped item with a
/// gold "NEW" badge — used by the Victory Screen.
/// </summary>
public partial class EquipmentPane : Control
{
    readonly EquippableItem? _highlightItem;

    // Live references for incremental refresh (slots are permanent; only inventory
    // flow is rebuilt when items move in or out).
    EquipSlotControl[] _slotControls  = Array.Empty<EquipSlotControl>();
    Label[]            _itemNameLabels = Array.Empty<Label>();
    HFlowContainer     _inventoryFlow  = null!;

    // ── colour palette (mirrors LoadoutController) ────────────────────────────
    static readonly Color TitleColor = new(0.95f, 0.84f, 0.50f);
    static readonly Color HintColor  = new(0.45f, 0.42f, 0.38f);
    static readonly Color SepColor   = new(0.50f, 0.40f, 0.22f, 0.55f);

    public EquipmentPane(EquippableItem? highlightItem = null)
    {
        _highlightItem = highlightItem;
    }

    public override void _Ready()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical   = SizeFlags.ExpandFill;

        var outer = new MarginContainer();
        outer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        outer.AddThemeConstantOverride("margin_left",   12);
        outer.AddThemeConstantOverride("margin_right",  12);
        outer.AddThemeConstantOverride("margin_top",     6);
        outer.AddThemeConstantOverride("margin_bottom",  6);
        AddChild(outer);

        var hbox = new HBoxContainer();
        hbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        hbox.SizeFlagsVertical   = SizeFlags.ExpandFill;
        hbox.AddThemeConstantOverride("separation", 20);
        outer.AddChild(hbox);

        hbox.AddChild(BuildSlotsSection());

        var vsep = new VSeparator();
        vsep.SizeFlagsVertical = SizeFlags.ExpandFill;
        vsep.AddThemeColorOverride("color", SepColor);
        hbox.AddChild(vsep);

        hbox.AddChild(BuildInventorySection());
    }

    // ── public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Refresh all slot icons and item-name labels, then rebuild the inventory
    /// flow from the current ItemStore state.  Call this whenever ItemStore changes.
    /// </summary>
    public void Refresh()
    {
        var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));
        for (var i = 0; i < slots.Length; i++)
        {
            _slotControls[i].Refresh();

            var equipped = ItemStore.GetEquipped(slots[i]);
            _itemNameLabels[i].Text = equipped?.Name ?? "— Empty —";
            _itemNameLabels[i].AddThemeColorOverride("font_color",
                equipped != null ? RarityColor(equipped.Rarity) : HintColor);
        }

        RebuildInventoryFlow();
    }

    // ── private builders ──────────────────────────────────────────────────────

    Control BuildSlotsSection()
    {
        var slots = (EquipSlot[])Enum.GetValues(typeof(EquipSlot));
        _slotControls   = new EquipSlotControl[slots.Length];
        _itemNameLabels = new Label[slots.Length];

        var vbox = new VBoxContainer();
        vbox.CustomMinimumSize = new Vector2(230f, 0f);
        vbox.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 6);

        var header = new Label();
        header.Text = "Equipment";
        header.AddThemeFontSizeOverride("font_size", 16);
        header.AddThemeColorOverride("font_color", TitleColor);
        vbox.AddChild(header);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", SepColor);
        vbox.AddChild(sep);

        for (var i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];

            var row = new HBoxContainer();
            row.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddThemeConstantOverride("separation", 12);

            // ── Slot frame control ────────────────────────────────────────────
            var slotCtrl = new EquipSlotControl(slot);
            slotCtrl.OnChanged = Refresh;
            _slotControls[i] = slotCtrl;
            row.AddChild(slotCtrl);

            // ── Labels to the right of the frame ─────────────────────────────
            var labelCol = new VBoxContainer();
            labelCol.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            labelCol.SizeFlagsVertical   = SizeFlags.ShrinkCenter;
            labelCol.AddThemeConstantOverride("separation", 3);
            labelCol.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(labelCol);

            var slotNameLabel = new Label();
            slotNameLabel.Text = EquipSlotControl.SlotDisplayName(slot);
            slotNameLabel.AddThemeFontSizeOverride("font_size", 11);
            slotNameLabel.AddThemeColorOverride("font_color", HintColor);
            slotNameLabel.MouseFilter = MouseFilterEnum.Ignore;
            labelCol.AddChild(slotNameLabel);

            var equipped = ItemStore.GetEquipped(slot);
            var itemNameLabel = new Label();
            itemNameLabel.Text = equipped?.Name ?? "— Empty —";
            itemNameLabel.AddThemeFontSizeOverride("font_size", 14);
            itemNameLabel.AddThemeColorOverride("font_color",
                equipped != null ? RarityColor(equipped.Rarity) : HintColor);
            itemNameLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            itemNameLabel.MouseFilter  = MouseFilterEnum.Ignore;
            labelCol.AddChild(itemNameLabel);
            _itemNameLabels[i] = itemNameLabel;

            vbox.AddChild(row);
        }

        // Push content to the top
        var spacer = new Control();
        spacer.SizeFlagsVertical = SizeFlags.ExpandFill;
        vbox.AddChild(spacer);

        return vbox;
    }

    Control BuildInventorySection()
    {
        var vbox = new VBoxContainer();
        vbox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        vbox.SizeFlagsVertical   = SizeFlags.ExpandFill;
        vbox.AddThemeConstantOverride("separation", 8);

        var header = new Label();
        header.Text = "Inventory";
        header.AddThemeFontSizeOverride("font_size", 16);
        header.AddThemeColorOverride("font_color", TitleColor);
        vbox.AddChild(header);

        var sep = new HSeparator();
        sep.AddThemeColorOverride("color", SepColor);
        vbox.AddChild(sep);

        // ── Scrollable inventory tile grid ────────────────────────────────────
        var scroll = new ScrollContainer();
        scroll.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.SizeFlagsVertical   = SizeFlags.ExpandFill;
        vbox.AddChild(scroll);

        _inventoryFlow = new HFlowContainer();
        _inventoryFlow.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _inventoryFlow.AddThemeConstantOverride("h_separation", 8);
        _inventoryFlow.AddThemeConstantOverride("v_separation", 8);
        scroll.AddChild(_inventoryFlow);

        // ── Unequip drop strip ────────────────────────────────────────────────
        var dropZone = new InventoryDropZoneControl();
        dropZone.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        dropZone.CustomMinimumSize   = new Vector2(0f, 34f);
        dropZone.OnChanged           = Refresh;
        vbox.AddChild(dropZone);

        // ── Usage hint ────────────────────────────────────────────────────────
        var hint = new Label();
        hint.Text = "Drag items to their slot to equip  •  Drag slots to inventory to unequip";
        hint.AutowrapMode       = TextServer.AutowrapMode.Word;
        hint.HorizontalAlignment = HorizontalAlignment.Center;
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", HintColor);
        vbox.AddChild(hint);

        RebuildInventoryFlow();
        return vbox;
    }

    void RebuildInventoryFlow()
    {
        foreach (var child in _inventoryFlow.GetChildren())
            child.QueueFree();

        if (ItemStore.Inventory.Count == 0)
        {
            var empty = new Label();
            empty.Text = "No items in inventory.\nItems drop from defeated bosses.";
            empty.AutowrapMode = TextServer.AutowrapMode.Word;
            empty.AddThemeFontSizeOverride("font_size", 13);
            empty.AddThemeColorOverride("font_color", HintColor);
            empty.MouseFilter = MouseFilterEnum.Ignore;
            _inventoryFlow.AddChild(empty);
            return;
        }

        foreach (var item in ItemStore.Inventory)
        {
            var isNew  = _highlightItem != null && ReferenceEquals(item, _highlightItem);
            var ctrl   = new InventoryItemControl(item, isNew);
            ctrl.OnUnequipDrop = Refresh;
            _inventoryFlow.AddChild(ctrl);
        }
    }

    // ── shared helper ─────────────────────────────────────────────────────────

    static Color RarityColor(ItemRarity rarity) => EquipSlotControl.RarityColor(rarity);
}

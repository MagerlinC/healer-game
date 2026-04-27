#nullable enable
using System;
using Godot;
using healerfantasy.Items;

namespace healerfantasy.UI;

/// <summary>
/// A styled strip at the bottom of the inventory section that acts as a
/// catch-all drop target for items being dragged out of equipment slots.
///
/// This ensures the player can always unequip by dragging — even when the
/// inventory panel is empty (no InventoryItemControl tiles to drop onto).
/// </summary>
public partial class InventoryDropZoneControl : Control
{
    /// <summary>Called after a successful unequip drop. Wire this to EquipmentPane.Refresh.</summary>
    public Action? OnChanged { get; set; }

    public override void _Ready()
    {
        var style = new StyleBoxFlat();
        style.BgColor = new Color(0.08f, 0.07f, 0.06f, 0.55f);
        style.SetBorderWidthAll(1);
        style.BorderColor = new Color(0.30f, 0.25f, 0.18f, 0.70f);
        style.SetCornerRadiusAll(4);
        style.ContentMarginLeft  = style.ContentMarginRight  = 8f;
        style.ContentMarginTop   = style.ContentMarginBottom = 6f;

        var panel = new PanelContainer();
        panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        panel.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(panel);

        var label = new Label();
        label.Text = "↑  Drag equipped items here to unequip";
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", new Color(0.42f, 0.39f, 0.35f));
        label.MouseFilter = MouseFilterEnum.Ignore;
        panel.AddChild(label);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.AsString() == "item_drag" && DragState.FromSlot != null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (DragState.FromSlot == null) return;
        ItemStore.Unequip(DragState.FromSlot.Value);
        DragState.Clear();
        OnChanged?.Invoke();
    }
}

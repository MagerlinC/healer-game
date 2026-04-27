#nullable enable
using System;
using Godot;
using healerfantasy.Items;

namespace healerfantasy.UI;

/// <summary>
/// A 64×68 draggable tile for an item sitting in the inventory (unequipped).
///
/// Drag-and-drop behaviour:
///   • Drag source: sets DragState so EquipSlotControl can equip the item.
///   • Drop target: accepts items being dragged OUT of an equipment slot,
///     calling ItemStore.Unequip and then OnUnequipDrop (→ EquipmentPane.Refresh).
///
/// Pass <paramref name="isHighlighted"/> = true on the Victory Screen for the
/// freshly-dropped item — draws a gold border and a "NEW" badge.
/// </summary>
public partial class InventoryItemControl : Control
{
	readonly EquippableItem _item;
	readonly bool _isHighlighted;

	/// <summary>Called after an equipped item is dropped onto this control (unequip).</summary>
	public Action? OnUnequipDrop { get; set; }

	public InventoryItemControl(EquippableItem item, bool isHighlighted = false)
	{
		_item = item;
		_isHighlighted = isHighlighted;
		// Leave a little extra height for the "NEW" badge when highlighted.
		CustomMinimumSize = new Vector2(64f, _isHighlighted ? 72f : 64f);
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override void _Ready()
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.09f, 0.07f, 0.07f, 0.90f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(2);
		style.BorderColor = EquipSlotControl.RarityColor(_item.Rarity);
		style.ContentMarginLeft = style.ContentMarginRight = 4f;
		style.ContentMarginTop = style.ContentMarginBottom = 4f;

		var panel = new PanelContainer();
		panel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		panel.SizeFlagsVertical = SizeFlags.ExpandFill;
		panel.AddThemeStyleboxOverride("panel", style);
		panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		panel.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(panel);

		var col = new VBoxContainer();
		col.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		col.SizeFlagsVertical = SizeFlags.ExpandFill;
		col.AddThemeConstantOverride("separation", 0);
		col.MouseFilter = MouseFilterEnum.Ignore;
		panel.AddChild(col);

		// Item icon
		var icon = new TextureRect();
		icon.Texture = _item.Icon;
		icon.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		icon.SizeFlagsVertical = SizeFlags.ExpandFill;
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.MouseFilter = MouseFilterEnum.Ignore;
		col.AddChild(icon);

		// "NEW" badge for victory-screen highlight
		if (_isHighlighted)
		{
			var badge = new Label();
			badge.Text = "NEW";
			badge.HorizontalAlignment = HorizontalAlignment.Center;
			badge.AddThemeFontSizeOverride("font_size", 8);
			badge.AddThemeColorOverride("font_color", new Color(0.95f, 0.84f, 0.50f));
			badge.MouseFilter = MouseFilterEnum.Ignore;
			col.AddChild(badge);
		}

		MouseEntered += () =>
			GameTooltip.Show(
				$"{_item.Name}\n{_item.Rarity}  •  {EquipSlotControl.SlotDisplayName(_item.Slot)}\n\n{_item.Description}");
		MouseExited += () => GameTooltip.Hide();
	}

	// ── drag source ───────────────────────────────────────────────────────────

	public override Variant _GetDragData(Vector2 atPosition)
	{
		DragState.Item = _item;
		DragState.FromSlot = null; // from inventory, not from a slot

		// Build a small drag-preview icon
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.08f, 0.07f, 0.07f, 0.88f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(2);
		style.BorderColor = EquipSlotControl.RarityColor(_item.Rarity);

		var preview = new PanelContainer();
		preview.CustomMinimumSize = new Vector2(52f, 52f);
		preview.AddThemeStyleboxOverride("panel", style);

		var previewIcon = new TextureRect();
		previewIcon.Texture = _item.Icon;
		previewIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		previewIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		previewIcon.MouseFilter = MouseFilterEnum.Ignore;
		preview.AddChild(previewIcon);

		SetDragPreview(preview);
		return "item_drag";
	}

	// ── drop target (accepts items dragged out of slots → unequip) ────────────

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.AsString() == "item_drag" && DragState.FromSlot != null;
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (DragState.FromSlot == null) return;
		ItemStore.Unequip(DragState.FromSlot.Value);
		DragState.Clear();
		OnUnequipDrop?.Invoke();
	}

	// ── drag-end cleanup ──────────────────────────────────────────────────────

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
			DragState.Clear();
	}
}
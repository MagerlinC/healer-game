#nullable enable
using System;
using Godot;
using healerfantasy.Items;

namespace healerfantasy.UI;

/// <summary>
/// A 64×64 equipment slot control backed by the equipment-slot.png frame asset.
///
/// Visual layers (back to front):
///   1. Rarity glow — coloured border when an item is equipped.
///   2. Frame texture — the decorative equipment-slot.png frame.
///   3. Item icon  — the equipped item's icon, inset 8 px inside the frame.
///
/// Drag-and-drop behaviour:
///   • Drag source: starts a drag when an item is equipped; sets DragState so
///     the drop target knows what is being moved.
///   • Drop target: accepts any item whose Slot matches this slot; calls
///     ItemStore.Equip and fires OnChanged.
/// </summary>
public partial class EquipSlotControl : Control
{
	// Lazy-loaded once across all instances.
	static Texture2D? _frameTexture;

	public EquipSlot Slot { get; }

	/// <summary>Fired after a successful drop onto (or out of) this slot.</summary>
	public Action? OnChanged { get; set; }

	StyleBoxFlat _glow = null!;
	TextureRect _itemIcon = null!;

	const float SlotSize = 64f;

	public EquipSlotControl(EquipSlot slot)
	{
		Slot = slot;
		CustomMinimumSize = new Vector2(SlotSize, SlotSize);
		MouseDefaultCursorShape = CursorShape.PointingHand;
	}

	public override void _Ready()
	{
		_frameTexture ??= GD.Load<Texture2D>(AssetConstants.EquipmentSlotFramePath);

		// ── Rarity glow (border behind frame) ────────────────────────────────
		_glow = new StyleBoxFlat();
		_glow.BgColor = Colors.Transparent;
		_glow.SetBorderWidthAll(2);
		_glow.BorderColor = Colors.Transparent;
		_glow.SetCornerRadiusAll(4);

		var glowPanel = new Panel();
		glowPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		glowPanel.MouseFilter = MouseFilterEnum.Ignore;
		glowPanel.AddThemeStyleboxOverride("panel", _glow);
		AddChild(glowPanel);

		// ── Frame texture ─────────────────────────────────────────────────────
		var frame = new TextureRect();
		frame.Texture = _frameTexture;
		frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		frame.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		frame.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		frame.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(frame);

		// ── Item icon (inset inside the frame) ────────────────────────────────
		_itemIcon = new TextureRect();
		_itemIcon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
		_itemIcon.OffsetLeft = 8f;
		_itemIcon.OffsetTop = 8f;
		_itemIcon.OffsetRight = -8f;
		_itemIcon.OffsetBottom = -8f;
		_itemIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		_itemIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		_itemIcon.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_itemIcon);

		Refresh();

		MouseEntered += () =>
		{
			var item = ItemStore.GetEquipped(Slot);
			if (item != null)
			{

				GameTooltip.Show(
					item.Name, $"{item.Rarity}  •  {SlotDisplayName(Slot)}\n\n{item.Description}");
			}
			else
			{

				GameTooltip.Show(SlotDisplayName(Slot) + " slot",
					$"Drag an item here to equip it");
			}

		};
		MouseExited += () => GameTooltip.Hide();
	}

	/// <summary>Refreshes the icon and rarity glow from the current ItemStore state.</summary>
	public void Refresh()
	{
		if (_itemIcon == null) return;
		var item = ItemStore.GetEquipped(Slot);
		_itemIcon.Texture = item?.Icon;
		_itemIcon.Visible = item?.Icon != null;
		_glow.BorderColor = item != null ? RarityColor(item.Rarity) : Colors.Transparent;
	}

	// ── drag source ───────────────────────────────────────────────────────────

	public override Variant _GetDragData(Vector2 atPosition)
	{
		var item = ItemStore.GetEquipped(Slot);
		if (item == null) return default;

		DragState.Item = item;
		DragState.FromSlot = Slot;
		SetDragPreview(BuildDragPreview(item));
		return "item_drag";
	}

	// ── drop target ───────────────────────────────────────────────────────────

	public override bool _CanDropData(Vector2 atPosition, Variant data)
	{
		return data.AsString() == "item_drag"
		       && DragState.Item != null
		       && SlotAcceptsItem(Slot, DragState.Item.Slot);
	}

	public override void _DropData(Vector2 atPosition, Variant data)
	{
		if (DragState.Item == null) return;
		// Pass this slot explicitly so rings can land in either Ring1 or Ring2.
		ItemStore.Equip(DragState.Item, Slot);
		DragState.Clear();
		Refresh();
		OnChanged?.Invoke();
	}

	// ── drag-end cleanup ──────────────────────────────────────────────────────

	public override void _Notification(int what)
	{
		if (what == NotificationDragEnd)
			DragState.Clear();
	}

	// ── helpers ───────────────────────────────────────────────────────────────

	static Control BuildDragPreview(EquippableItem item)
	{
		var style = new StyleBoxFlat();
		style.BgColor = new Color(0.08f, 0.07f, 0.07f, 0.88f);
		style.SetCornerRadiusAll(4);
		style.SetBorderWidthAll(2);
		style.BorderColor = RarityColor(item.Rarity);

		var container = new PanelContainer();
		container.CustomMinimumSize = new Vector2(52f, 52f);
		container.AddThemeStyleboxOverride("panel", style);

		var icon = new TextureRect();
		icon.Texture = item.Icon;
		icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		icon.MouseFilter = MouseFilterEnum.Ignore;
		container.AddChild(icon);

		return container;
	}

	/// <summary>
	/// Returns true when <paramref name="targetSlot"/> can accept an item whose
	/// canonical slot is <paramref name="itemSlot"/>.
	///
	/// Ring items (Ring1 or Ring2) are interchangeable — they can go into either
	/// ring slot.  All other slot types must match exactly.
	/// </summary>
	static bool SlotAcceptsItem(EquipSlot targetSlot, EquipSlot itemSlot)
	{
		var targetIsRing = targetSlot == EquipSlot.Ring1 || targetSlot == EquipSlot.Ring2;
		var itemIsRing = itemSlot == EquipSlot.Ring1 || itemSlot == EquipSlot.Ring2;
		if (targetIsRing && itemIsRing) return true;
		return targetSlot == itemSlot;
	}

	/// <summary>Returns a player-facing name for a slot (e.g. Ring1 → "Ring").</summary>
	internal static string SlotDisplayName(EquipSlot slot)
	{
		return slot switch
		{
			EquipSlot.Ring1 => "Ring",
			EquipSlot.Ring2 => "Ring",
			_ => slot.ToString()
		};
	}

	internal static Color RarityColor(ItemRarity rarity)
	{
		return rarity switch
		{
			ItemRarity.Rare => new Color(0.35f, 0.55f, 1.00f),
			ItemRarity.Epic => new Color(0.70f, 0.30f, 0.90f),
			ItemRarity.Legendary => new Color(1.00f, 0.55f, 0.05f),
			_ => new Color(0.80f, 0.78f, 0.72f)
		};
	}
}
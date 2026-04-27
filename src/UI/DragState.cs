using healerfantasy.Items;

namespace healerfantasy.UI;

/// <summary>
/// Tiny static bag that carries the currently-dragged item between the drag
/// source (_GetDragData) and the drop target (_DropData / _CanDropData).
///
/// Cleared by the drag source in _Notification(NotificationDragEnd) whether
/// or not the drop was accepted, so it never stays stale.
/// </summary>
public static class DragState
{
    /// <summary>The item currently being dragged. Null when no drag is active.</summary>
    public static EquippableItem? Item;

    /// <summary>
    /// The slot the item was dragged FROM, or null if dragged from the inventory.
    /// Used by drop targets to know whether to call Equip or Unequip.
    /// </summary>
    public static EquipSlot? FromSlot;

    public static void Clear()
    {
        Item = null;
        FromSlot = null;
    }
}

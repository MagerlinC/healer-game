using System.Collections.Generic;
using System.Linq;

namespace healerfantasy.Items;

/// <summary>
/// Run-scoped item storage — a lightweight static singleton that tracks the
/// player's item inventory and equipped items for the duration of one run.
///
/// All state is cleared at the start of each new run via <see cref="Clear"/>.
/// Items are not persisted to disk; they exist only within a run and are
/// logged by <see cref="RunHistoryStore"/> when the run ends.
///
/// Inventory = found but unequipped items.
/// Equipped   = one item per <see cref="EquipSlot"/> (the player wears it actively).
/// </summary>
public static class ItemStore
{
    static readonly List<EquippableItem> _inventory = new();
    static readonly Dictionary<EquipSlot, EquippableItem> _equipped = new();

    public static IReadOnlyList<EquippableItem> Inventory => _inventory.AsReadOnly();
    public static IReadOnlyDictionary<EquipSlot, EquippableItem> Equipped => _equipped;

    /// <summary>True if the player has at least one item (equipped or in inventory).</summary>
    public static bool HasAnyItems => _inventory.Count > 0 || _equipped.Count > 0;

    // ── mutations ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Add a newly-dropped item to the unequipped inventory.
    /// The player can later equip it via the Armory or immediately on the
    /// VictoryScreen.
    /// </summary>
    public static void AddToInventory(EquippableItem item) => _inventory.Add(item);

    /// <summary>
    /// Equip <paramref name="item"/> into its <see cref="EquipSlot"/>.
    /// Any item previously occupying that slot is moved back to inventory.
    /// </summary>
    public static void Equip(EquippableItem item)
    {
        if (_equipped.TryGetValue(item.Slot, out var displaced))
            _inventory.Add(displaced);
        _equipped[item.Slot] = item;
        _inventory.Remove(item);
    }

    /// <summary>Move the equipped item in <paramref name="slot"/> back to inventory.</summary>
    public static void Unequip(EquipSlot slot)
    {
        if (_equipped.TryGetValue(slot, out var item))
        {
            _inventory.Add(item);
            _equipped.Remove(slot);
        }
    }

    // ── queries ───────────────────────────────────────────────────────────────

    public static EquippableItem? GetEquipped(EquipSlot slot) =>
        _equipped.TryGetValue(slot, out var item) ? item : null;

    public static IEnumerable<EquippableItem> GetEquippedItems() => _equipped.Values;

    /// <summary>
    /// Returns display names of all currently-equipped items.
    /// Used by <see cref="RunHistoryStore"/> to log items before clearing state.
    /// </summary>
    public static List<string> GetEquippedItemNames() =>
        _equipped.Values.Select(i => i.Name).ToList();

    // ── lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Reset all item state. Call at the start of each new run and after a run
    /// is finalised and logged.
    /// </summary>
    public static void Clear()
    {
        _inventory.Clear();
        _equipped.Clear();
    }
}

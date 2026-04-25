using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.Items.Staves;

namespace healerfantasy.Items;

/// <summary>
/// Central loot table mapping boss names to the items they can drop.
///
/// Each entry is a factory <see cref="Func{T}"/> so every roll produces a
/// fresh item instance (sharing instances would mean equipping the same object
/// twice, which would break modifier lists).
///
/// Adding a new item:
///   1. Create a subclass of <see cref="EquippableItem"/> in src/Items/.
///   2. Add a factory entry here with the restricting boss name (or null for any boss).
///   3. Done — the loot roller picks from the eligible pool automatically.
/// </summary>
public static class ItemRegistry
{
    /// <summary>
    /// (BossName | null, factory).
    /// null BossName = item may drop from any boss.
    /// </summary>
    static readonly List<(string? BossName, Func<EquippableItem> Factory)> LootTable =
    [
        (GameConstants.Boss1Name, () => new CrystalStaff()),
        (GameConstants.Boss2Name, () => new DeathweaveStaff()),
        (GameConstants.Boss3Name, () => new SlimewardenSceptre()),
        (null,                    () => new ArcaneAccelerator()),
        (null,                    () => new StaffOfEternalFlame()),
    ];

    /// <summary>Overall chance that any boss drops an item (0–1).</summary>
    const float DropChance = 0.70f;

    /// <summary>
    /// Roll the loot table for the given boss.
    /// Returns a fresh item instance, or null when the drop roll fails.
    ///
    /// The eligible pool is all entries whose BossName matches
    /// <paramref name="bossName"/> plus all any-boss entries.
    /// One item is chosen uniformly at random from the pool.
    /// </summary>
    public static EquippableItem? RollDrop(string bossName)
    {
        if (GD.Randf() > DropChance) return null;

        var pool = LootTable
            .Where(e => e.BossName == null || e.BossName == bossName)
            .ToList();

        if (pool.Count == 0) return null;

        var idx = (int)(GD.Randi() % (uint)pool.Count);
        return pool[idx].Factory();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy.Items.Amulets;
using healerfantasy.Items.Rings;
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
		// Staves
		(GameConstants.Boss1Name, () => new CrystalStaff()),
		(GameConstants.Boss2Name, () => new DeathweaveStaff()),
		(GameConstants.Boss3Name, () => new SlimewardenSceptre()),
		(GameConstants.SanctumBoss1Name, () => new VoidWeaver()),
		(null, () => new ArcaneAccelerator()),
		(null, () => new StaffOfEternalFlame()),

		// Rings
		(null, () => new RulersSignet()),
		(null, () => new BandOfTheVoid()),

		// Amulets
		(null, () => new TheHeartOfLight())

	];

	const float NothingWeight = 0.3f;
	const float RareWeight = 0.3f;
	const float EpicWeight = 0.25f;
	const float LegendaryWeight = 0.15f;

	static ItemRarity? RollRarity()
	{
		var roll = GD.Randf();
		if (roll < NothingWeight) return null;
		return roll switch
		{
			< RareWeight => ItemRarity.Rare,
			< RareWeight + EpicWeight => ItemRarity.Epic,
			_ => ItemRarity.Legendary
		};
	}

	public static EquippableItem? RollDrop(string bossName)
	{

		var rarity = RollRarity();
		if (rarity == null) return null;

		var bossPool = LootTable
			.Where(e => e.BossName == bossName)
			.Where(e =>
			{
				var item = e.Factory();
				return item.Rarity == rarity && !ItemStore.HasFoundItem(item.ItemId);
			})
			.ToList();

		// Prefer boss drops
		if (bossPool.Count > 0)
		{
			var idx = (int)(GD.Randi() % (uint)bossPool.Count);
			return bossPool[idx].Factory();
		}

		var genericPool = LootTable
			.Where(e => e.BossName == null)
			.Where(e =>
			{
				var item = e.Factory();
				return item.Rarity == rarity && !ItemStore.HasFoundItem(item.ItemId);
			})
			.ToList();
		if (genericPool.Count > 0)
		{
			var idx = (int)(GD.Randi() % (uint)genericPool.Count);
			return genericPool[idx].Factory();
		}

		return null;
	}
}
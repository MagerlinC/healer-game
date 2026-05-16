#nullable enable
using System.Collections.Generic;
using System.Linq;
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources;

namespace healerfantasy.Merchant;

/// <summary>
/// Run-scoped static store for the Merchant's state.
///
/// Tracks which shop items have been purchased and whether the Stone of
/// Rebirth has been consumed mid-run. Also holds the randomly-generated
/// item stock for the current camp visit. All state is wiped at run end via
/// <see cref="Clear"/> (called from GlobalAutoLoad.Reset).
///
/// Sell/buy prices are defined here so that <see cref="UI.MerchantPane"/>
/// and any future systems share a single source of truth.
/// </summary>
public static class MerchantStore
{
    // ── Stone of Rebirth state ─────────────────────────────────────────────

    /// <summary>True once the player has bought the Stone of Rebirth this run.</summary>
    public static bool StoneOfRebirthPurchased { get; private set; } = false;

    /// <summary>True once the Stone has triggered and been consumed.</summary>
    public static bool StoneOfRebirthConsumed { get; private set; } = false;

    /// <summary>The stone is purchased and still waiting to trigger.</summary>
    public static bool StoneOfRebirthActive => StoneOfRebirthPurchased && !StoneOfRebirthConsumed;

    /// <summary>Buy price of the Stone of Rebirth.</summary>
    public const int StoneOfRebirthPrice = 70;

    // ── Item stock ────────────────────────────────────────────────────────

    static readonly List<EquippableItem> _stock = new();

    /// <summary>Items currently for sale. Populated by <see cref="GenerateStock"/>.</summary>
    public static IReadOnlyList<EquippableItem> Stock => _stock.AsReadOnly();

    /// <summary>
    /// Randomly generates 0–3 items from the boss-drop pool for the merchant
    /// to sell this camp visit.  Should be called once per camp from
    /// <c>CampController.SetupScene</c>.
    ///
    /// Uses <see cref="ItemRegistry.RollDrop"/> with a dummy boss name so only
    /// generic (non-boss-exclusive) items are eligible. Already-found items and
    /// duplicates within the stock are excluded automatically.
    /// </summary>
    public static void GenerateStock()
    {
        _stock.Clear();

        var targetCount = (int)GD.RandRange(0, 4); // 0, 1, 2, or 3
        var maxAttempts = targetCount * 8;           // avoid infinite loop if pool is thin

        for (var attempt = 0; attempt < maxAttempts && _stock.Count < targetCount; attempt++)
        {
            var item = ItemRegistry.RollDrop("merchant"); // "merchant" matches no boss → generic pool
            if (item == null) continue;
            if (_stock.Any(s => s.ItemId == item.ItemId)) continue; // no duplicates in stock
            _stock.Add(item);
        }
    }

    /// <summary>
    /// Remove <paramref name="item"/> from the merchant's stock after purchase.
    /// The item is added to the player's inventory and gold is deducted.
    /// Returns false if the player cannot afford it.
    /// </summary>
    public static bool BuyStockItem(EquippableItem item)
    {
        var price = BuyPrice(item.Rarity);
        if (!RunState.Instance.SpendGold(price)) return false;
        _stock.Remove(item);
        ItemStore.AddToInventory(item);
        return true;
    }

    // ── Pricing ───────────────────────────────────────────────────────────

    public const int RareSellPrice      = 20;
    public const int EpicSellPrice      = 50;
    public const int LegendarySellPrice = 100;

    /// <summary>Gold the player receives when selling an item of this rarity.</summary>
    public static int SellPrice(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Rare      => RareSellPrice,
        ItemRarity.Epic      => EpicSellPrice,
        ItemRarity.Legendary => LegendarySellPrice,
        _                    => 0
    };

    /// <summary>
    /// Gold the merchant charges when the player buys an item of this rarity.
    /// Always 1.5× the sell price, rounded to the nearest integer.
    /// </summary>
    public static int BuyPrice(ItemRarity rarity) =>
        (int)System.Math.Round(SellPrice(rarity) * 1.5f);

    // ── Stone of Rebirth actions ──────────────────────────────────────────

    /// <summary>
    /// Called when the player buys the Stone of Rebirth.
    /// Registers it with <see cref="ItemEffectBus"/> so the effect bar shows
    /// it as active during subsequent combat encounters.
    /// </summary>
    public static void PurchaseStoneOfRebirth()
    {
        StoneOfRebirthPurchased = true;
        var icon = GD.Load<Texture2D>(AssetConstants.StoneOfRebirthIconPath);
        ItemEffectBus.Activate(
            "stone_of_rebirth",
            icon,
            "Stone of Rebirth",
            "The next character who falls in battle is revived at 50% health.");
    }

    /// <summary>
    /// Called by the revival hook in <see cref="Character"/> when the stone
    /// triggers. Removes it from the effect bar.
    /// </summary>
    public static void ConsumeStoneOfRebirth()
    {
        StoneOfRebirthConsumed = true;
        ItemEffectBus.Deactivate("stone_of_rebirth");
    }

    /// <summary>
    /// Attempt to revive <paramref name="character"/> using the Stone of Rebirth.
    /// Returns true (and revives at 50% HP) if the stone was active and the
    /// character is a party member; false otherwise.
    /// </summary>
    public static bool TryRevive(Character character)
    {
        if (!StoneOfRebirthActive) return false;
        if (!character.IsInGroup(GameConstants.PartyGroupName)) return false;

        ConsumeStoneOfRebirth();

        // Bring the character back at 50% of their maximum health.
        // Revive() bypasses the IsAlive guard that Heal() has, which is required
        // here because CurrentHealth is 0 at the point OnDeath() calls us.
        var reviveAmount = character.MaxHealth * 0.5f;
        character.Revive(reviveAmount);

        // Show a golden heal number so the revival is visible.
        character.RaiseFloatingCombatText(reviveAmount, true, (int)SpellSchool.Holy, false);

        return true;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reset all merchant state. Called from GlobalAutoLoad.Reset() at run end
    /// so stale purchases don't carry over into the next run.
    /// </summary>
    public static void Clear()
    {
        StoneOfRebirthPurchased = false;
        StoneOfRebirthConsumed = false;
        _stock.Clear();
        // Effect bar cleanup is handled by ItemEffectBus.Reset() which is also
        // called from GlobalAutoLoad.Reset().
    }
}

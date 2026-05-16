#nullable enable
using Godot;
using healerfantasy;
using healerfantasy.Items;
using healerfantasy.SpellResources;

namespace healerfantasy.Merchant;

/// <summary>
/// Run-scoped static store for the Merchant's state.
///
/// Tracks which shop items have been purchased and whether the Stone of
/// Rebirth has been consumed mid-run. All state is wiped at run end via
/// <see cref="Clear"/> (called from GlobalAutoLoad.Reset).
///
/// Sell prices are defined here so that both <see cref="UI.MerchantPane"/>
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
    public const int StoneOfRebirthPrice = 75;

    // ── Sell prices by rarity ──────────────────────────────────────────────

    public const int RareSellPrice = 20;
    public const int EpicSellPrice = 50;
    public const int LegendarySellPrice = 100;

    public static int SellPrice(ItemRarity rarity) => rarity switch
    {
        ItemRarity.Rare => RareSellPrice,
        ItemRarity.Epic => EpicSellPrice,
        ItemRarity.Legendary => LegendarySellPrice,
        _ => 0
    };

    // ── Actions ───────────────────────────────────────────────────────────

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
        float reviveAmount = character.MaxHealth * 0.5f;
        character.Heal(reviveAmount);

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
        // Effect bar cleanup is handled by ItemEffectBus.Reset() which is also
        // called from GlobalAutoLoad.Reset().
    }
}

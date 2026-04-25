using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Crystal Staff — Rare staff dropped exclusively by the Crystal Knight.
/// Carved from a shard of the knight's crystalline armour; channels mana with
/// unnatural efficiency.
///
/// Stat bonus: +25 maximum mana.
/// </summary>
public class CrystalStaff : EquippableItem
{
    public override string ItemId => "crystal_staff";

    public CrystalStaff()
    {
        Name        = "Crystal Staff";
        Description = "+25 maximum mana.";
        Rarity      = ItemRarity.Rare;
        Slot        = EquipSlot.Staff;
        Icon        = GD.Load<Texture2D>(AssetConstants.StaveIconPath(1));
        CharacterModifiers.Add(new MaxManaModifier());
    }

    class MaxManaModifier : ICharacterModifier
    {
        public void Modify(CharacterStats stats) => stats.MaxMana += 25f;
    }
}

using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Deathweave Staff — Rare staff dropped exclusively by the Bringer of Death.
/// Woven from shadow-thread saturated with necromantic energy; paradoxically
/// amplifies restorative magic.
///
/// Stat bonus: +15% healing multiplier.
/// </summary>
public class DeathweaveStaff : EquippableItem
{
    public override string ItemId => "deathweave_staff";

    public DeathweaveStaff()
    {
        Name        = "Deathweave Staff";
        Description = "+15% healing multiplier.";
        Rarity      = ItemRarity.Rare;
        Slot        = EquipSlot.Staff;
        Icon        = GD.Load<Texture2D>(AssetConstants.StaveIconPath(2));
        CharacterModifiers.Add(new HealingModifier());
    }

    class HealingModifier : ICharacterModifier
    {
        public void Modify(CharacterStats stats) => stats.HealingMultiplier *= 1.15f;
    }
}

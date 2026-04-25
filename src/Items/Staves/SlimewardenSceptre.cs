using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Slimewarden Sceptre — Rare sceptre dropped exclusively by the Demon Slime.
/// Infused with volatile arcane ooze that sharpens magical focus to a razor's
/// edge, dramatically increasing the chance of critical spell strikes.
///
/// Stat bonus: +10% critical strike chance.
/// </summary>
public class SlimewardenSceptre : EquippableItem
{
    public override string ItemId => "slimewarden_sceptre";

    public SlimewardenSceptre()
    {
        Name        = "Slimewarden Sceptre";
        Description = "+10% critical strike chance.";
        Rarity      = ItemRarity.Rare;
        Slot        = EquipSlot.Staff;
        Icon        = GD.Load<Texture2D>(AssetConstants.StaveIconPath(3));
        CharacterModifiers.Add(new CritModifier());
    }

    class CritModifier : ICharacterModifier
    {
        public void Modify(CharacterStats stats) => stats.CritChance += 0.10f;
    }
}

using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Crystal ring — Rare staff dropped exclusively by the Crystal Knight.
/// Carved from a shard of the knight's crystalline armour; channels mana with
/// unnatural efficiency.
///
/// Stat bonus: +25 maximum mana.
/// </summary>
public class CrystalRing : EquippableItem
{
	public override string ItemId => "crystal_ring";

	public CrystalRing()
	{
		Name = "Crystal ring";
		Description = "+20 maximum mana.";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Ring1;
		Icon = GD.Load<Texture2D>(AssetConstants.RingIconPath(5));
		CharacterModifiers.Add(new MaxManaModifier());
	}

	class MaxManaModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.MaxMana += 20f;
		}
	}
}
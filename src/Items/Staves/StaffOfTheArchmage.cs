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
public class StaffOfTheArchmage : EquippableItem
{
	static float MaxManaIncrease = 50f;
	static float ManaRegenIncrease = 2f;
	static float ManaCostIncreasePercent = 0.5f;
	public override string ItemId => "staff_of_the_archmage";

	public StaffOfTheArchmage()
	{
		Name = "Staff of the Archmage";
		Description =
			$"+{MaxManaIncrease:F0} maximum mana. +{ManaRegenIncrease:F} mana regenerated per second. Spells cost {100 * ManaCostIncreasePercent:F0}% more mana.";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(7));
		CharacterModifiers.Add(new MaxManaModifier());
	}

	class MaxManaModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.MaxMana += MaxManaIncrease;
			stats.ManaRegenPerSecond += ManaRegenIncrease;
			stats.ManaCostMultiplier += ManaCostIncreasePercent;
		}
	}
}
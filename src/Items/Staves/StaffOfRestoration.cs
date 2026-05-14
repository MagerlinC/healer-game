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
public class StaffOfRestoration : EquippableItem
{
	static float HealingIncrease = 0.15f;
	public override string ItemId => "staff_of_restoration";

	public StaffOfRestoration()
	{
		Name = "Staff of Restoration";
		Description = $"{100 * HealingIncrease:F0}% increased healing";
		Rarity = ItemRarity.Rare;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(2));
		CharacterModifiers.Add(new HealingModifier());
	}

	class HealingModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedHealing += 0.15f;
		}
	}
}
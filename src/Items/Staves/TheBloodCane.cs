using System;
using Godot;
using healerfantasy.SpellResources;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Arcane Accelerator — Epic staff that can drop from any boss.
/// Imbued with time-warping enchantments that compress the gap between thought
/// and spell, while also amplifying offensive output.
///
/// Stat bonuses: +15% haste, +10% damage multiplier.
/// </summary>
public class TheBloodCane : EquippableItem
{
	static readonly float LifeCostMultiplier = 0.25f;
	public override string ItemId => "the_blood_cane";

	public TheBloodCane()
	{
		Name = "The Blood Cane";
		Description =
			$"Sanguimancy spells have {100 * LifeCostMultiplier:F0}% increased life cost, but also do {100 * LifeCostMultiplier:F0}% more damage and healing";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(8));
		CharacterModifiers.Add(new SanguimancyEffect());
	}

	class SanguimancyEffect : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.LifeCostMultiplier *= 1f + LifeCostMultiplier;
			stats.SpellSchoolIncreasedDamage[SpellSchool.Sanguimancy] += LifeCostMultiplier;
			stats.SpellSchoolIncreasedHealing[SpellSchool.Sanguimancy] += LifeCostMultiplier;
		}
	}
}
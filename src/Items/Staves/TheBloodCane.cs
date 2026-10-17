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
			$"Pyre spells burn {100 * LifeCostMultiplier:F0}% more of your own light, but also deal {100 * LifeCostMultiplier:F0}% more damage and restore {100 * LifeCostMultiplier:F0}% more light";
		Rarity = ItemRarity.Legendary;
		Slot = EquipSlot.Staff;
		School = SpellSchool.Pyre;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(8));
		CharacterModifiers.Add(new PyreEffect());
	}

	class PyreEffect : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.LifeCostMultiplier *= 1f + LifeCostMultiplier;
			stats.SpellSchoolIncreasedDamage[SpellSchool.Pyre] += LifeCostMultiplier;
			stats.SpellSchoolIncreasedHealing[SpellSchool.Pyre] += LifeCostMultiplier;
		}
	}
}
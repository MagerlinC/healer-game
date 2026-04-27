using System;
using Godot;
using healerfantasy.SpellSystem;

namespace healerfantasy.Items.Staves;

/// <summary>
/// Arcane Accelerator — Epic staff that can drop from any boss.
/// Imbued with time-warping enchantments that compress the gap between thought
/// and spell, while also amplifying offensive output.
///
/// Stat bonuses: +15% haste, +10% damage multiplier.
/// </summary>
public class ArcaneAccelerator : EquippableItem
{
	static readonly float _hasteAmount = 0.10f;
	public override string ItemId => "arcane_accelerator";

	public ArcaneAccelerator()
	{
		Name = "Arcane Accelerator";
		Description = $"+{Math.Round(_hasteAmount * 100)}% haste.\n10% increased damage.";
		Rarity = ItemRarity.Epic;
		Slot = EquipSlot.Staff;
		Icon = GD.Load<Texture2D>(AssetConstants.StaveIconPath(4));
		CharacterModifiers.Add(new SpeedDamageModifier());
	}

	class SpeedDamageModifier : ICharacterModifier
	{
		public void Modify(CharacterStats stats)
		{
			stats.IncreasedHaste += _hasteAmount;
			stats.IncreasedDamage += 0.10f;
		}
	}
}